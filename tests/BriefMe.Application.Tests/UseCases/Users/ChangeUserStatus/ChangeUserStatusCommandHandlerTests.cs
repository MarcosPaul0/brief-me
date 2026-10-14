using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.ChangeUserStatus;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.ChangeUserStatus;

public class ChangeUserStatusCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly ChangeUserStatusCommandHandler _handler;

    public ChangeUserStatusCommandHandlerTests()
    {
        _handler = new ChangeUserStatusCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);
    }

    private static User CreateExistingUser() =>
        User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hash", UserRole.Requester);

    [Fact]
    public async Task Handle_WhenDeactivatingExistingUser_UpdatesStatusRevokesTokensAndCommits()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangeUserStatusCommand(user.Id, UserStatus.Inactive, "127.0.0.1");

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Inactive);
        _refreshTokenRepositoryMock.Verify(r => r.RevokeAllActiveForUserAsync(user.Id, "127.0.0.1", default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenActivatingExistingUser_UpdatesStatusAndDoesNotRevokeTokens()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangeUserStatusCommand(user.Id, UserStatus.Active);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        _refreshTokenRepositoryMock.Verify(
            r => r.RevokeAllActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangeUserStatusCommand(userId, UserStatus.Inactive);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
        _refreshTokenRepositoryMock.Verify(
            r => r.RevokeAllActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLoggedUserTriesToDeactivateSelf_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangeUserStatusCommand(user.Id, UserStatus.Inactive);

        _currentUserMock.Setup(c => c.UserId).Returns(user.Id.ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
        _refreshTokenRepositoryMock.Verify(
            r => r.RevokeAllActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLoggedUserActivatesSelf_IsAllowed()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangeUserStatusCommand(user.Id, UserStatus.Active);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _currentUserMock.Setup(c => c.UserId).Returns(user.Id.ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_SkipsSelfDeactivationCheck()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new ChangeUserStatusCommand(user.Id, UserStatus.Inactive);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Inactive);
    }
}
