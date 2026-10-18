using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Users.UpdateUser;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.UpdateUser;

public class UpdateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _handler = new UpdateUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<Guid?>(), default))
            .ReturnsAsync(false);
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());
    }

    private static User CreateExistingUser(UserRole role = UserRole.Requester) =>
        User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password", role);

    [Fact]
    public async Task Handle_WhenDataIsValid_UpdatesProfileAndRole()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new UpdateUserCommand(user.Id, "Ana Souza", "ana.souza@briefme.com", UserRole.Designer);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new { FullName = "Ana Souza", Email = "ana.souza@briefme.com", Role = UserRole.Designer });
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAdminChangesOwnRole_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var admin = CreateExistingUser(UserRole.Admin);
        var command = new UpdateUserCommand(admin.Id, admin.FullName, admin.Email.Value, UserRole.Designer);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(admin.Id, default)).ReturnsAsync(admin);
        _currentUserMock.Setup(c => c.UserId).Returns(admin.Id.ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Error.Should().Be(UserErrors.CannotChangeOwnRole);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAdminEditsOwnProfileKeepingRole_Succeeds()
    {
        // Arrange
        var admin = CreateExistingUser(UserRole.Admin);
        var command = new UpdateUserCommand(admin.Id, "Novo Nome", admin.Email.Value, UserRole.Admin);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(admin.Id, default)).ReturnsAsync(admin);
        _currentUserMock.Setup(c => c.UserId).Returns(admin.Id.ToString());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNewEmailAlreadyUsedByAnotherUser_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new UpdateUserCommand(user.Id, user.FullName, "taken@briefme.com", UserRole.Requester);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), user.Id, default)).ReturnsAsync(true);

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
        var command = new UpdateUserCommand(userId, "Ana Silva", "ana@briefme.com", UserRole.Requester);

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailIsMalformed_ReturnsFailureWithoutLoadingUser()
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.NewGuid(), "Ana Silva", "not-an-email", UserRole.Requester);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
