using BriefMe.Application.UseCases.Users.UpdateUser;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.UpdateUser;

public class UpdateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _handler = new UpdateUserCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _unitOfWorkMock.Object);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<Guid?>(), default))
            .ReturnsAsync(false);
    }

    private static User CreateExistingUser(string fullName = "Ana Silva", string email = "ana@briefme.com") =>
        User.Create(fullName, Email.Create(email).Value, "hashed-password");

    [Fact]
    public async Task Handle_WhenDataIsValidAndRoleIdsDiffer_UpdatesProfileAndReconcilesRoles()
    {
        // Arrange
        var currentRole = Role.Create("Designer", null);
        var newRole = Role.Create("Gestor de Design", null);
        var user = CreateExistingUser();
        user.AssignRole(currentRole.Id);

        var command = new UpdateUserCommand(user.Id, "Ana Souza", "ana.souza@briefme.com", [newRole.Id]);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepositoryMock.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(newRole.Id)), default))
            .ReturnsAsync([newRole]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Ana Souza");
        result.Value.Email.Should().Be("ana.souza@briefme.com");
        user.UserRoles.Select(ur => ur.RoleId).Should().BeEquivalentTo([newRole.Id]);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRoleIdsIsEmpty_RemovesAllCurrentRoles()
    {
        // Arrange
        var currentRole = Role.Create("Designer", null);
        var user = CreateExistingUser();
        user.AssignRole(currentRole.Id);

        var command = new UpdateUserCommand(user.Id, user.FullName, user.Email.Value, []);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserRoles.Should().BeEmpty();
        _roleRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNewEmailAlreadyUsedByAnotherUser_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var user = CreateExistingUser();
        var command = new UpdateUserCommand(user.Id, user.FullName, "taken@briefme.com", []);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);
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
        var command = new UpdateUserCommand(userId, "Ana Silva", "ana@briefme.com", []);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(userId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<Guid?>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSomeRoleIdsDoNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var existingRole = Role.Create("Designer", null);
        var missingRoleId = Guid.NewGuid();
        var user = CreateExistingUser();
        var command = new UpdateUserCommand(user.Id, user.FullName, user.Email.Value, [existingRole.Id, missingRoleId]);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default)).ReturnsAsync([existingRole]);

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
        var command = new UpdateUserCommand(Guid.NewGuid(), "Ana Silva", "not-an-email", []);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
