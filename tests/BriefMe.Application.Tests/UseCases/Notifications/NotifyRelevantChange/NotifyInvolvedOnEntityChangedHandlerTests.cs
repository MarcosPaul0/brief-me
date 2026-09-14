using System.Globalization;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.UseCases.Notifications.NotifyRelevantChange;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.NotifyRelevantChange;

public class NotifyInvolvedOnEntityChangedHandlerTests
{
    private const string TaskTitle = "Banner para o evento";
    private const string BriefingTitle = "Campanha de fim de ano";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly Guid _otherManagerId = Guid.NewGuid();
    private readonly NotifyInvolvedOnEntityChangedHandler _handler;

    public NotifyInvolvedOnEntityChangedHandlerTests()
    {
        _handler = new NotifyInvolvedOnEntityChangedHandler(
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
        var briefing = Briefing.Create(
            BriefingTitle,
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças para o encerramento do ano.",
            objective: "Divulgar o evento",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));

        briefing.SubmitForApproval();
        briefing.Approve(_reviewerId);
        briefing.StartExecution();

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private BriefingTask ArrangeAssignedTask(Briefing briefing)
    {
        var task = BriefingTask.Create(briefing.Id, TaskTitle, Priority.High);

        task.AssignTo(_designerId, _reviewerId);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    private void ArrangeBriefingTasks(Briefing briefing, params BriefingTask[] tasks) =>
        _taskRepositoryMock
            .Setup(r => r.GetByBriefingIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

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

    private static EntityChanged TaskEvent(BriefingTask task, EntityChangeKind change, Guid? changedById = null) =>
        new(DemandSubject.Task, task.Id, task.BriefingId, change, changedById);

    private static EntityChanged BriefingEvent(Briefing briefing, EntityChangeKind change) =>
        new(DemandSubject.Briefing, briefing.Id, briefing.Id, change, ChangedById: null);

    [Fact]
    public async Task Handle_WhenTaskIsReassigned_NotifiesTheRequesterAndTheReviewingManager()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        task.ReassignTo(Guid.NewGuid(), _otherManagerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.Reassigned, _otherManagerId), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_requesterId, _reviewerId]);
    }

    [Fact]
    public async Task Handle_WhenTaskIsReassigned_DoesNotNotifyTheNewDesigner()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        var newDesignerId = Guid.NewGuid();
        task.ReassignTo(newDesignerId, _otherManagerId);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.Reassigned, _otherManagerId), default);

        // Assert
        batches.Single().Should().NotContain(request => request.RecipientId == newDesignerId);
    }

    [Fact]
    public async Task Handle_WhenTheAuthorIsAlsoInvolved_DoesNotNotifyTheAuthor()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        task.ReassignTo(Guid.NewGuid(), _reviewerId);
        var batches = CaptureBatches();

        // Act — the Manager who reviewed the briefing is the one performing the transfer.
        await _handler.Handle(TaskEvent(task, EntityChangeKind.Reassigned, _reviewerId), default);

        // Assert
        batches.Single().Select(request => request.RecipientId).Should().BeEquivalentTo([_requesterId]);
    }

    [Fact]
    public async Task Handle_WhenTheChangeHasNoKnownAuthor_NotifiesEveryoneInvolved()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.StatusChanged), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_requesterId, _reviewerId, _designerId]);
    }

    [Fact]
    public async Task Handle_WhenARoleIsHeldTwice_NotifiesThePersonOnce()
    {
        // Arrange — the requester is also the Designer assigned to the task.
        var briefing = ArrangeBriefingUnderExecution();
        var task = BriefingTask.Create(briefing.Id, TaskTitle, Priority.High);
        task.AssignTo(_requesterId, _reviewerId);
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.StatusChanged), default);

        // Assert
        batches.Single().Should().ContainSingle(request => request.RecipientId == _requesterId);
    }

    [Fact]
    public async Task Handle_WhenAChangeIsAnnounced_UsesTheEntityChangedEventType()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.StatusChanged), default);

        // Assert
        batches.Single().Should()
            .OnlyContain(request => request.EventType == NotificationEventType.EntityChanged);
    }

    [Fact]
    public async Task Handle_WhenSeveralPeopleAreInvolved_DeliversASingleBatch()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.StatusChanged), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.IsAny<IReadOnlyCollection<NotificationRequest>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTaskStatusChanges_MessageNamesTheTaskTheBriefingAndTheNewStatus()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.StatusChanged), default);

        // Assert
        batches.Single().First().Message.Should()
            .Contain(TaskTitle).And
            .Contain(BriefingTitle).And
            .Contain("Aguardando aprovação técnica");
    }

    [Fact]
    public async Task Handle_WhenTaskIsReplanned_MessageNamesTheNewDeadlineAndPriority()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = ArrangeAssignedTask(briefing);
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        task.SetDeadlineAndPriority(deadline, Priority.Urgent);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.Replanned), default);

        // Assert
        batches.Single().First().Message.Should()
            .Contain(deadline.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).And
            .Contain("Urgente");
    }

    [Fact]
    public async Task Handle_WhenTheBriefingIsCompleted_NotifiesTheDesignersOfItsTasks()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        var task = BriefingTask.Create(briefing.Id, TaskTitle, Priority.High);
        task.AssignTo(_designerId, _reviewerId);
        ArrangeBriefingTasks(briefing, task);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(BriefingEvent(briefing, EntityChangeKind.Completed), default);

        // Assert
        batches.Single().Select(request => request.RecipientId)
            .Should().BeEquivalentTo([_requesterId, _reviewerId, _designerId]);
    }

    [Fact]
    public async Task Handle_WhenTheBriefingChanges_MessageNamesTheBriefing()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        ArrangeBriefingTasks(briefing);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(BriefingEvent(briefing, EntityChangeKind.Completed), default);

        // Assert
        batches.Single().First().Message.Should().Contain(BriefingTitle);
    }

    [Fact]
    public async Task Handle_WhenTheBriefingHasNoReviewerYet_NotifiesTheOthers()
    {
        // Arrange — edited while awaiting a first review: SubmitForApproval cleared the decision.
        var briefing = Briefing.Create(
            BriefingTitle,
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças para o encerramento do ano.",
            objective: "Divulgar o evento",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        briefing.SubmitForApproval();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);
        ArrangeBriefingTasks(briefing);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(BriefingEvent(briefing, EntityChangeKind.Edited), default);

        // Assert
        batches.Single().Select(request => request.RecipientId).Should().BeEquivalentTo([_requesterId]);
    }

    [Fact]
    public async Task Handle_WhenNobodyIsLeftToNotify_DoesNotCallTheService()
    {
        // Arrange — a briefing awaiting its first review, edited by its own requester: no reviewer has been
        // registered yet and no task exists, so the author is the only person involved.
        var briefing = Briefing.Create(
            BriefingTitle,
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças para o encerramento do ano.",
            objective: "Divulgar o evento",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        briefing.SubmitForApproval();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);
        ArrangeBriefingTasks(briefing);

        // Act
        await _handler.Handle(
            new EntityChanged(
                DemandSubject.Briefing,
                briefing.Id,
                briefing.Id,
                EntityChangeKind.Edited,
                _requesterId),
            default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.IsAny<IReadOnlyCollection<NotificationRequest>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheSubjectPairHasNoWording_DoesNotNotify()
    {
        // Arrange — no aggregate raises a briefing-level reassignment.
        var briefing = ArrangeBriefingUnderExecution();

        // Act
        await _handler.Handle(BriefingEvent(briefing, EntityChangeKind.Reassigned), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.IsAny<IReadOnlyCollection<NotificationRequest>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_DoesNotNotify()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        await _handler.Handle(
            new EntityChanged(
                DemandSubject.Briefing,
                Guid.NewGuid(),
                Guid.NewGuid(),
                EntityChangeKind.Completed,
                ChangedById: null),
            default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.IsAny<IReadOnlyCollection<NotificationRequest>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskNotFound_DoesNotNotify()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        await _handler.Handle(
            new EntityChanged(
                DemandSubject.Task,
                Guid.NewGuid(),
                briefing.Id,
                EntityChangeKind.StatusChanged,
                ChangedById: null),
            default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.IsAny<IReadOnlyCollection<NotificationRequest>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskNotFound_DoesNotThrow()
    {
        // Arrange
        var briefing = ArrangeBriefingUnderExecution();
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var handling = async () => await _handler.Handle(
            new EntityChanged(
                DemandSubject.Task,
                Guid.NewGuid(),
                briefing.Id,
                EntityChangeKind.StatusChanged,
                ChangedById: null),
            default);

        // Assert
        await handling.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenTitlesAreAtTheirLimits_ComposesATextNotificationAccepts()
    {
        // Arrange
        var briefing = Briefing.Create(
            new string('b', 200),
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças",
            objective: "Divulgar",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        briefing.SubmitForApproval();
        briefing.Approve(_reviewerId);
        briefing.StartExecution();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        var task = BriefingTask.Create(briefing.Id, new string('t', 200), Priority.High);
        task.AssignTo(_designerId, _reviewerId);
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        task.SetDeadlineAndPriority(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), Priority.Urgent);
        var batches = CaptureBatches();

        // Act
        await _handler.Handle(TaskEvent(task, EntityChangeKind.Replanned), default);

        // Assert
        var request = batches.Single().First();
        var creating = () => Notification.Create(
            request.RecipientId,
            request.EventType,
            request.Title,
            request.Message);
        creating.Should().NotThrow();
    }
}
