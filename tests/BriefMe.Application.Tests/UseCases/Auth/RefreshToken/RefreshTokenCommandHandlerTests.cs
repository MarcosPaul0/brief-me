using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Auth.RefreshToken;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Auth.RefreshToken;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IJwtService> _jwtServiceMock = new();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _refreshTokenServiceMock.Object,
            _userRepositoryMock.Object,
            _jwtServiceMock.Object);
    }

    private static User CreateActiveUser(string fullName = "Ana Silva", string email = "ana@briefme.com") =>
        User.Create(fullName, Email.Create(email).Value, "hashed-password");

    [Fact]
    public async Task Handle_WhenRefreshTokenIsActive_ReturnsSuccessWithNewTokensAndCurrentClaims()
    {
        // Arrange
        var user = CreateActiveUser();
        var command = new RefreshTokenCommand("current-token", "127.0.0.1");
        var rotated = new RotatedRefreshToken("new-refresh-token", user.Id);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        _refreshTokenServiceMock.Setup(s => s.RotateAsync(command.RefreshToken, command.IpAddress, default))
            .ReturnsAsync(Result.Success(rotated));
        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAndPermissionsAsync(user.Id, default)).ReturnsAsync(user);
        _jwtServiceMock.Setup(j => j.GenerateAccessToken(user)).Returns(("access-token", expiresAt));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        result.Value.ExpiresIn.Should().BeGreaterThan(0);
        result.Value.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenIsExpiredOrRevoked_ReturnsFailureWithoutGeneratingTokens()
    {
        // Arrange
        var command = new RefreshTokenCommand("invalid-token");

        _refreshTokenServiceMock.Setup(s => s.RotateAsync(command.RefreshToken, command.IpAddress, default))
            .ReturnsAsync(Result.Failure<RotatedRefreshToken>("Token revogado."));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Refresh token inválido ou expirado.");
        _userRepositoryMock.Verify(r => r.GetByIdWithRolesAndPermissionsAsync(It.IsAny<Guid>(), default), Times.Never);
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsInactiveAfterRotation_RevokesNewTokenAndReturnsFailure()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate();
        var command = new RefreshTokenCommand("current-token", "127.0.0.1");
        var rotated = new RotatedRefreshToken("new-refresh-token", user.Id);

        _refreshTokenServiceMock.Setup(s => s.RotateAsync(command.RefreshToken, command.IpAddress, default))
            .ReturnsAsync(Result.Success(rotated));
        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAndPermissionsAsync(user.Id, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _refreshTokenServiceMock.Verify(s => s.RevokeAsync("new-refresh-token", command.IpAddress, default), Times.Once);
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserNoLongerExistsAfterRotation_RevokesNewTokenAndReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand("current-token");
        var rotated = new RotatedRefreshToken("new-refresh-token", userId);

        _refreshTokenServiceMock.Setup(s => s.RotateAsync(command.RefreshToken, command.IpAddress, default))
            .ReturnsAsync(Result.Success(rotated));
        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAndPermissionsAsync(userId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _refreshTokenServiceMock.Verify(s => s.RevokeAsync("new-refresh-token", command.IpAddress, default), Times.Once);
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }
}
