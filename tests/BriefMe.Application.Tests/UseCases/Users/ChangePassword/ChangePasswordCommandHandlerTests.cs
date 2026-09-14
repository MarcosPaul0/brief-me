using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.ChangePassword;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.ChangePassword;

public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _handler = new ChangePasswordCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object);
    }

    private static User CreateExistingUser(string passwordHash = "old-hash") =>
        User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, passwordHash);

    [Fact]
    public async Task Handle_WhenCurrentPasswordIsCorrectAndNewPasswordIsValid_UpdatesHashAndRevokesSessions()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangePasswordCommand(user.Id, "OldPass1", "NewPass1", "127.0.0.1");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify("OldPass1", user.PasswordHash)).Returns(true);
        _passwordHasherMock.Setup(h => h.Verify("NewPass1", user.PasswordHash)).Returns(false);
        _passwordHasherMock.Setup(h => h.Hash("NewPass1")).Returns("new-hash");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new-hash");
        _refreshTokenRepositoryMock.Verify(r => r.RevokeAllActiveForUserAsync(user.Id, "127.0.0.1", default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCurrentPasswordIsIncorrect_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangePasswordCommand(user.Id, "WrongPass1", "NewPass1");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify("WrongPass1", user.PasswordHash)).Returns(false);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
        _refreshTokenRepositoryMock.Verify(
            r => r.RevokeAllActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentPasswordIsNull_SkipsCurrentPasswordVerification()
    {
        // Arrange
        var user = CreateExistingUser();
        var originalHash = user.PasswordHash;
        var command = new ChangePasswordCommand(user.Id, null, "NewPass1");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify("NewPass1", originalHash)).Returns(false);
        _passwordHasherMock.Setup(h => h.Hash("NewPass1")).Returns("new-hash");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _passwordHasherMock.Verify(h => h.Verify(It.IsAny<string>(), originalHash), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNewPasswordFailsComplexityPolicy_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangePasswordCommand(user.Id, "OldPass1", "weak");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify("OldPass1", user.PasswordHash)).Returns(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNewPasswordMatchesCurrentPassword_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangePasswordCommand(user.Id, "OldPass1", "OldPass1");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify("OldPass1", user.PasswordHash)).Returns(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangePasswordCommand(userId, "OldPass1", "NewPass1");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _passwordHasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
