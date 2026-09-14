using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Notifications.GetMyNotifications;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.GetMyNotifications;

public class GetMyNotificationsQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly GetMyNotificationsQueryHandler _handler;

    private readonly Guid _recipientId = Guid.NewGuid();

    public GetMyNotificationsQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_recipientId.ToString());

        _handler = new GetMyNotificationsQueryHandler(
            _notificationRepositoryMock.Object,
            _currentUserMock.Object);
    }

    private static Notification NewNotification(
        Guid recipientId,
        NotificationEventType eventType = NotificationEventType.BriefingApproved,
        string title = "Briefing aprovado",
        string message = "Seu briefing \"Campanha de Natal\" foi aprovado.") =>
        Notification.Create(recipientId, eventType, title, message);

    [Fact]
    public async Task Handle_WhenTheInboxHasEntries_ReturnsThemWithPaginationMetadata()
    {
        // Arrange
        var notification = NewNotification(_recipientId);

        _notificationRepositoryMock
            .Setup(r => r.GetByRecipientAsync(_recipientId, 2, 10, default))
            .ReturnsAsync([notification]);

        _notificationRepositoryMock
            .Setup(r => r.CountByRecipientAsync(_recipientId, default))
            .ReturnsAsync(23);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(2, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(23);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(notification.Id);
        item.EventType.Should().Be(NotificationEventType.BriefingApproved);
        item.Title.Should().Be("Briefing aprovado");
        item.Message.Should().Be("Seu briefing \"Campanha de Natal\" foi aprovado.");
        item.CreatedAt.Should().Be(notification.CreatedAt);
    }

    [Fact]
    public async Task Handle_WhenAnEntryIsUnread_ReturnsItWithoutAReadInstant()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.GetByRecipientAsync(_recipientId, 1, 10, default))
            .ReturnsAsync([NewNotification(_recipientId)]);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(1, 10), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ReadAt.Should().BeNull();
        item.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAnEntryWasAlreadyRead_ReturnsItWithTheReadInstant()
    {
        // Arrange
        var notification = NewNotification(_recipientId);
        notification.MarkAsRead();

        _notificationRepositoryMock
            .Setup(r => r.GetByRecipientAsync(_recipientId, 1, 10, default))
            .ReturnsAsync([notification]);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(1, 10), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ReadAt.Should().Be(notification.ReadAt);
        item.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheInboxIsEmpty_ReturnsAnEmptyPage()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.GetByRecipientAsync(_recipientId, 1, 10, default))
            .ReturnsAsync([]);

        _notificationRepositoryMock
            .Setup(r => r.CountByRecipientAsync(_recipientId, default))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(1, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(1, 10), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be(CommonErrors.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAuthenticated_NeverReadsTheInbox()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns("not-a-guid");

        // Act
        await _handler.Handle(new GetMyNotificationsQuery(1, 10), default);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.GetByRecipientAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheInboxIsRead_ScopesItToTheAuthenticatedRecipient()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.GetByRecipientAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default))
            .ReturnsAsync([]);

        // Act
        await _handler.Handle(new GetMyNotificationsQuery(1, 10), default);

        // Assert
        // The recipient is never an input; this is the guard that keeps one inbox out of another's reach.
        _notificationRepositoryMock.Verify(r => r.GetByRecipientAsync(_recipientId, 1, 10, default), Times.Once);
    }
}
