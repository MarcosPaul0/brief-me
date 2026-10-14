using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Auth.Login;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Auth.Login;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtService> _jwtServiceMock = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object);
    }

    private static User CreateActiveUser(string fullName = "Ana Silva", string email = "ana@briefme.com", string passwordHash = "hashed-password")
    {
        return User.Create(fullName, Email.Create(email).Value, passwordHash, UserRole.Requester);
    }

    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ReturnsSuccessWithTokensAndUserData()
    {
        // Arrange
        var user = CreateActiveUser();
        var command = new LoginCommand(user.Email.Value, "correct-password", "127.0.0.1");
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify(command.Password, user.PasswordHash)).Returns(true);
        _jwtServiceMock.Setup(j => j.GenerateAccessToken(user)).Returns(("access-token", expiresAt));
        _refreshTokenServiceMock.Setup(r => r.GenerateAsync(user.Id, command.IpAddress, default)).ReturnsAsync("refresh-token");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.ExpiresIn.Should().BeGreaterThan(0);
        result.Value.UserId.Should().Be(user.Id);
        result.Value.FullName.Should().Be(user.FullName);
        result.Value.Email.Should().Be(user.Email.Value);
    }

    [Fact]
    public async Task Handle_WhenEmailDoesNotExist_ReturnsGenericFailureWithoutGeneratingTokens()
    {
        // Arrange
        var command = new LoginCommand("unknown@briefme.com", "any-password");

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("E-mail ou senha inválidos.");
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        _refreshTokenServiceMock.Verify(r => r.GenerateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsIncorrect_ReturnsSameGenericFailureAsUnknownEmail()
    {
        // Arrange
        var user = CreateActiveUser();
        var command = new LoginCommand(user.Email.Value, "wrong-password");

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify(command.Password, user.PasswordHash)).Returns(false);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("E-mail ou senha inválidos.");
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        _refreshTokenServiceMock.Verify(r => r.GenerateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsFailureWithoutVerifyingPasswordOrGeneratingTokens()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate();
        var command = new LoginCommand(user.Email.Value, "correct-password");

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _passwordHasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        _refreshTokenServiceMock.Verify(r => r.GenerateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailIsMalformed_ReturnsGenericFailureWithoutQueryingRepository()
    {
        // Arrange
        var command = new LoginCommand("not-an-email", "any-password");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("E-mail ou senha inválidos.");
        _userRepositoryMock.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), default), Times.Never);
        _jwtServiceMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }
}
