using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.UseCases.Notifications.NotifyTaskAssignment;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.NotifyTaskAssignment;

public class NotifyDesignerOnTaskAssignedHandlerTests
{
    private const string TaskTitle = "Banner para o evento";
    private const string BriefingTitle = "Campanha de fim de ano";

    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly NotifyDesignerOnTaskAssignedHandler _handler;

    public NotifyDesignerOnTaskAssignedHandlerTests()
    {
        _handler = new NotifyDesignerOnTaskAssignedHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _notificationServiceMock.Object);
    }

    /// <summary>Task assigned through the real transition (no reflection), with its briefing readable.</summary>
    private BriefingTask ArrangeAssignedTask()
    {
        var briefing = ArrangeBriefing();
        var task = BriefingTask.Create(briefing.Id, TaskTitle, Priority.High);

        task.AssignTo(_designerId, _managerId);
        ArrangeTaskRead(task);

        return task;
    }

    private Briefing ArrangeBriefing()
    {
        var briefing = Briefing.Create(BriefingTitle, Priority.High, Guid.NewGuid(), Guid.NewGuid());

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private void ArrangeTaskRead(BriefingTask task) =>
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

    private TaskAssigned EventFor(BriefingTask task) =>
        new(task.Id, task.BriefingId, _designerId, _managerId);

    [Fact]
    public async Task Handle_WhenTaskIsAssigned_NotifiesTheAssignedDesigner()
    {
        // Arrange
        var task = ArrangeAssignedTask();

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.RecipientId == _designerId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTaskIsAssigned_DoesNotNotifyTheAssigner()
    {
        // Arrange
        var task = ArrangeAssignedTask();

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.RecipientId == _managerId),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskIsAssigned_UsesTheTaskAssignedEventType()
    {
        // Arrange
        var task = ArrangeAssignedTask();

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.EventType == NotificationEventType.TaskAssigned),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTaskIsAssigned_MessageNamesTheTaskAndTheBriefing()
    {
        // Arrange
        var task = ArrangeAssignedTask();

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r =>
                    r.Message.Contains(TaskTitle) && r.Message.Contains(BriefingTitle)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTaskIsReassigned_NotifiesTheNewDesigner()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var task = BriefingTask.Create(briefing.Id, TaskTitle, Priority.High);
        task.AssignTo(Guid.NewGuid(), _managerId);
        task.ReassignTo(_designerId, _managerId);
        ArrangeTaskRead(task);

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r =>
                    r.RecipientId == _designerId && r.EventType == NotificationEventType.TaskAssigned),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_StillNotifiesNamingTheTask()
    {
        // Arrange
        var task = BriefingTask.Create(Guid.NewGuid(), TaskTitle, Priority.High);
        task.AssignTo(_designerId, _managerId);
        ArrangeTaskRead(task);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.Message.Contains(TaskTitle)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTaskNotFound_DoesNotNotify()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        await _handler.Handle(new TaskAssigned(Guid.NewGuid(), Guid.NewGuid(), _designerId, _managerId), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskNotFound_DoesNotThrow()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var handling = async () => await _handler.Handle(
            new TaskAssigned(Guid.NewGuid(), Guid.NewGuid(), _designerId, _managerId),
            default);

        // Assert
        await handling.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenTitlesAreAtTheirLimits_ComposesATextNotificationAccepts()
    {
        // Arrange
        var briefing = Briefing.Create(new string('b', 200), Priority.High, Guid.NewGuid(), Guid.NewGuid());
        var task = BriefingTask.Create(briefing.Id, new string('t', 200), Priority.High);
        task.AssignTo(_designerId, _managerId);
        ArrangeTaskRead(task);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        NotificationRequest? captured = null;
        _notificationServiceMock
            .Setup(s => s.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(EventFor(task), default);

        // Assert
        var creating = () => Notification.Create(
            captured!.RecipientId,
            captured.EventType,
            captured.Title,
            captured.Message);
        creating.Should().NotThrow();
    }
}
