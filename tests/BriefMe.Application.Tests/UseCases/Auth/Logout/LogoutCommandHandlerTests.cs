using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Auth.Logout;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Auth.Logout;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(
            _refreshTokenServiceMock.Object,
            _refreshTokenRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSingleSessionLogout_RevokesGivenTokenAndReturnsSuccess()
    {
        // Arrange
        var command = new LogoutCommand("active-refresh-token", AllDevices: false, IpAddress: "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenServiceMock.Verify(s => s.RevokeAsync("active-refresh-token", "127.0.0.1", default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenAlreadyRevokedOrUnknown_StillReturnsSuccess()
    {
        // Arrange
        var command = new LogoutCommand("already-revoked-token");

        _refreshTokenServiceMock
            .Setup(s => s.RevokeAsync(command.RefreshToken, command.IpAddress, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAllDevicesAndUserIsAuthenticated_RevokesAllActiveTokensAndCommits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LogoutCommand(AllDevices: true, IpAddress: "127.0.0.1");

        _currentUserMock.Setup(c => c.UserId).Returns(userId.ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenRepositoryMock.Verify(r => r.RevokeAllActiveForUserAsync(userId, "127.0.0.1", default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAllDevicesAndNoAuthenticatedUser_ReturnsFailureWithoutRevoking()
    {
        // Arrange
        var command = new LogoutCommand(AllDevices: true);

        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _refreshTokenRepositoryMock.Verify(r => r.RevokeAllActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAllDevices_IgnoresRefreshTokenFromRequestBody()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LogoutCommand("irrelevant-token", AllDevices: true);

        _currentUserMock.Setup(c => c.UserId).Returns(userId.ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenServiceMock.Verify(s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<string?>(), default), Times.Never);
    }
}
