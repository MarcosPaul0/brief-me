using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.UseCases.Notifications.NotifyOverdue;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.NotifyOverdue;

public class NotifyResponsiblesOnDeadlineOverdueHandlerTests
{
    private const string TaskTitle = "Banner para o evento";
    private const string BriefingTitle = "Campanha de fim de ano";

    /// <summary>Cap both title columns share (`BriefingConfiguration`, `BriefingTaskConfiguration`).</summary>
    private const int TitleColumnLength = 200;

    private static readonly DateTime DueAtUtc = new(2026, 9, 5, 14, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime DetectedAtUtc = new(2026, 9, 6, 3, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly NotifyResponsiblesOnDeadlineOverdueHandler _handler;

    public NotifyResponsiblesOnDeadlineOverdueHandlerTests()
    {
        _handler = new NotifyResponsiblesOnDeadlineOverdueHandler(
            _briefingRepositoryMock.Object,
            _taskRepositoryMock.Object,
            _notificationServiceMock.Object);
    }

    /// <summary>
    /// A briefing carried to <c>InProgress</c> through the real transitions, so <c>ReviewedById</c> holds the
    /// reviewer the way the aggregate fills it — never by reflection.
    /// </summary>
    private Briefing ArrangeBriefingUnderExecution()
    {
        var briefing = ArrangeDraft();

        briefing.SubmitForApproval();
        briefing.Approve(_reviewerId);
        briefing.StartExecution();

        return ArrangeBriefingRead(briefing);
    }

    /// <summary>
    /// A briefing still awaiting its first review: <c>ReviewedById</c> is null, which is the state the
    /// collective review queue leaves it in (RF030 marks it late all the same).
    /// </summary>
    private Briefing ArrangeBriefingAwaitingApproval()
    {
        var briefing = ArrangeDraft();

        briefing.SubmitForApproval();

        return ArrangeBriefingRead(briefing);
    }

    private Briefing ArrangeDraft() =>
        Briefing.Create(
            BriefingTitle,
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças para o encerramento do ano.",
            objective: "Divulgar o evento",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

    private Briefing ArrangeBriefingRead(Briefing briefing)
    {
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private BriefingTask ArrangeTask(Briefing briefing, Guid? designerId)
    {
        var task = BriefingTask.Create(briefing.Id, TaskTitle, Priority.High);

        if (designerId is { } assignee)
            task.AssignTo(assignee, _reviewerId);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    /// <summary>Captures the single batch the handler is expected to deliver.</summary>
    private List<IReadOnlyCollection<NotificationRequest>> CaptureBatches()
    {
        var batches = new List<IReadOnlyCollection<NotificationRequest>>();

        _notificationServiceMock
            .Setup(s => s.NotifyAsync(
                It.IsAny<IReadOnlyCollection<NotificationRequest>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<NotificationRequest>, CancellationToken>(
                (requests, _) => batches.Add(requests))
            .Returns(Task.CompletedTask);

        return batches;
    }

    /// <summary>
    /// The event the aggregate itself raises when the monitor marks the task, read back off the aggregate so
    /// the test never restates what <c>MarkOverdue</c> puts in it.
    /// </summary>
    private static DeadlineOverdue TaskEvent(BriefingTask task)
    {
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        return task.DomainEvents.OfType<DeadlineOverdue>().Single();
    }

    private static DeadlineOverdue BriefingEvent(Briefing briefing)
    {
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        return briefing.DomainEvents.OfType<DeadlineOverdue>().Single();
    }

    [Fact]
    public async Task Handle_WhenAnAssignedTaskIsOverdue_NotifiesTheAssignedDesigner()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Single().Should().ContainSingle(request => request.RecipientId == _designerId);
    }

    [Fact]
    public async Task Handle_WhenAnAssignedTaskIsOverdue_NotifiesTheReviewingManagerAsWell()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_designerId, _reviewerId]);
    }

    [Fact]
    public async Task Handle_WhenAnUnassignedTaskIsOverdue_NotifiesTheReviewingManagerAlone()
    {
        // Arrange — planned but never assigned (RF017), so the event carries no responsible party.
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, designerId: null);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_reviewerId]);
    }

    [Fact]
    public async Task Handle_WhenTheManagerIsAlsoTheAssignee_NotifiesThePersonOnce()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _reviewerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Single().Should().ContainSingle(request => request.RecipientId == _reviewerId);
    }

    [Fact]
    public async Task Handle_WhenAnOverdueTaskIsAnnounced_NamesTheTaskTheBriefingAndTheDueDate()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Single().First().Message
            .Should().Contain(TaskTitle).And.Contain(BriefingTitle).And.Contain("05/09/2026");
    }

    [Fact]
    public async Task Handle_WhenAnOverdueTaskIsAnnounced_UsesTheDeadlineOverdueEventType()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Single().Should().OnlyContain(
            request => request.EventType == NotificationEventType.DeadlineOverdue);
    }

    [Fact]
    public async Task Handle_WhenSeveralPeopleAnswerForTheDelay_DeliversASingleBatch()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        batches.Should().ContainSingle();
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnOverdueBriefingIsAnnounced_NotifiesTheRequesterAndTheReviewingManager()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(BriefingEvent(briefing), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_requesterId, _reviewerId]);
    }

    [Fact]
    public async Task Handle_WhenAnOverdueBriefingHasNoDecisionYet_NotifiesTheRequesterAlone()
    {
        // Arrange — sitting unreviewed past the requested date is the delay RF030 exists to surface.
        var briefing = ArrangeBriefingAwaitingApproval();
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(BriefingEvent(briefing), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_requesterId]);
    }

    [Fact]
    public async Task Handle_WhenAnOverdueBriefingIsAnnounced_NamesTheBriefingAndTheDueDate()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(BriefingEvent(briefing), default);

        // Assert
        batches.Single().First().Message
            .Should().Contain(BriefingTitle).And.Contain("05/09/2026");
    }

    [Fact]
    public async Task Handle_WhenTheBriefingCannotBeRead_DoesNotNotify()
    {
        // Arrange — the briefing is the source of the title and of the Manager.
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var domainEvent = TaskEvent(task);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        _notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheTaskCannotBeRead_DoesNotNotify()
    {
        // Arrange — without the task there is no subject left for the sentence.
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var domainEvent = TaskEvent(task);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        _notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheDemandCannotBeRead_DoesNotThrow()
    {
        // Arrange — one sweep queues one event per marked demand; throwing would cost all the others.
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeTask(briefing, _designerId);
        var domainEvent = TaskEvent(task);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var act = () => _handler.Handle(domainEvent, default);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenNobodyAnswersForTheDelay_DoesNotCallTheNotificationService()
    {
        // Arrange — an unassigned task on a briefing with no recorded decision leaves no recipient.
        var briefing = ArrangeBriefingAwaitingApproval();
        var task = ArrangeTask(briefing, designerId: null);

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert
        _notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenBothTitlesAreAtTheirLimit_ProducesANotificationTheEntityAccepts()
    {
        // Arrange — the worst case the catalog can be handed: both titles at the 200 their EF
        // configurations allow. This is what justifies the absence of a length cut in the wording.
        var longTitle = new string('x', TitleColumnLength);

        // The mandatory RF005 fields are what SubmitForApproval requires; without them the briefing would
        // never leave Draft and Approve would throw instead of the assertion running.
        var briefing = Briefing.Create(
            longTitle,
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças para o encerramento do ano.",
            objective: "Divulgar o evento",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        briefing.SubmitForApproval().IsSuccess.Should().BeTrue();
        briefing.Approve(_reviewerId);
        briefing.StartExecution();
        ArrangeBriefingRead(briefing);

        var task = BriefingTask.Create(briefing.Id, longTitle, Priority.High);
        task.AssignTo(_designerId, _reviewerId);
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task), default);

        // Assert — Notification.Create is the real gate: it throws past the limits.
        var request = batches.Single().First();
        var act = () => Notification.Create(
            request.RecipientId,
            request.EventType,
            request.Title,
            request.Message);

        act.Should().NotThrow();
    }
}
