using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Notifications.MarkNotificationsAsRead;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.MarkNotificationsAsRead;

public class MarkNotificationsAsReadCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly MarkNotificationsAsReadCommandHandler _handler;

    private readonly Guid _recipientId = Guid.NewGuid();

    public MarkNotificationsAsReadCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_recipientId.ToString());

        _handler = new MarkNotificationsAsReadCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);
    }

    private static Notification NewNotification(Guid recipientId) =>
        Notification.Create(
            recipientId,
            NotificationEventType.TaskAssigned,
            "Nova tarefa atribuída",
            "A tarefa \"Banner topo\" foi atribuída a você.");

    [Fact]
    public async Task Handle_WhenTheCallerHasUnreadEntries_MarksEveryOneOfThem()
    {
        // Arrange
        var first = NewNotification(_recipientId);
        var second = NewNotification(_recipientId);

        _notificationRepositoryMock
            .Setup(r => r.GetUnreadByRecipientAsync(_recipientId, default))
            .ReturnsAsync([first, second]);

        // Act
        var result = await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        first.IsRead.Should().BeTrue();
        second.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenEntriesAreMarked_CommitsOnce()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.GetUnreadByRecipientAsync(_recipientId, default))
            .ReturnsAsync([NewNotification(_recipientId), NewNotification(_recipientId)]);

        // Act
        await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        // The bulk gesture is one transaction, not one per entry.
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNothingIsUnread_CommitsNothing()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.GetUnreadByRecipientAsync(_recipientId, default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnEntryWasAlreadyRead_KeepsTheFirstReadInstant()
    {
        // Arrange
        var notification = NewNotification(_recipientId);
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;

        _notificationRepositoryMock
            .Setup(r => r.GetUnreadByRecipientAsync(_recipientId, default))
            .ReturnsAsync([notification]);

        // Act
        await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        // Opening the bell twice must not rewrite when the entry was read (BRF-047).
        notification.ReadAt.Should().Be(firstReadAt);
    }

    [Fact]
    public async Task Handle_WhenTheMarkIsApplied_ScopesItToTheAuthenticatedRecipient()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.GetUnreadByRecipientAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([]);

        // Act
        await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        _notificationRepositoryMock.Verify(r => r.GetUnreadByRecipientAsync(_recipientId, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be(CommonErrors.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAuthenticated_WritesNothing()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns("not-a-guid");

        // Act
        await _handler.Handle(new MarkNotificationsAsReadCommand(), default);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.GetUnreadByRecipientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
