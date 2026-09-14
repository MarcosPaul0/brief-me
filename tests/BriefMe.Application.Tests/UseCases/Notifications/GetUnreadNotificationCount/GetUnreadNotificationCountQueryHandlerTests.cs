using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Notifications.GetUnreadNotificationCount;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.GetUnreadNotificationCount;

public class GetUnreadNotificationCountQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly GetUnreadNotificationCountQueryHandler _handler;

    private readonly Guid _recipientId = Guid.NewGuid();

    public GetUnreadNotificationCountQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_recipientId.ToString());

        _handler = new GetUnreadNotificationCountQueryHandler(
            _notificationRepositoryMock.Object,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_WhenTheCallerHasUnreadEntries_ReturnsHowMany()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.CountUnreadByRecipientAsync(_recipientId, default))
            .ReturnsAsync(7);

        // Act
        var result = await _handler.Handle(new GetUnreadNotificationCountQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(7);
    }

    [Fact]
    public async Task Handle_WhenTheInboxIsCaughtUp_ReturnsZero()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.CountUnreadByRecipientAsync(_recipientId, default))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(new GetUnreadNotificationCountQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheCountIsRead_ScopesItToTheAuthenticatedRecipient()
    {
        // Arrange
        _notificationRepositoryMock
            .Setup(r => r.CountUnreadByRecipientAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(new GetUnreadNotificationCountQuery(), default);

        // Assert
        _notificationRepositoryMock.Verify(r => r.CountUnreadByRecipientAsync(_recipientId, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(new GetUnreadNotificationCountQuery(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be(CommonErrors.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAuthenticated_NeverCountsAnything()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        await _handler.Handle(new GetUnreadNotificationCountQuery(), default);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.CountUnreadByRecipientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
