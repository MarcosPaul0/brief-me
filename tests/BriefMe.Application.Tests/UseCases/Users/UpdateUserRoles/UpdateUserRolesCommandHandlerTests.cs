using BriefMe.Application.UseCases.Users.UpdateUserRoles;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.UpdateUserRoles;

public class UpdateUserRolesCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateUserRolesCommandHandler _handler;

    public UpdateUserRolesCommandHandlerTests()
    {
        _handler = new UpdateUserRolesCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    private static User CreateExistingUser() =>
        User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");

    [Fact]
    public async Task Handle_WhenRoleIdsExist_ReplacesCurrentRolesWithTargetSet()
    {
        // Arrange
        var keptRole = Role.Create("Designer", null);
        var removedRole = Role.Create("Solicitante", null);
        var addedRole = Role.Create("Gestor de Design", null);
        var user = CreateExistingUser();
        user.AssignRole(keptRole.Id);
        user.AssignRole(removedRole.Id);

        var command = new UpdateUserRolesCommand(user.Id, [keptRole.Id, addedRole.Id]);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(keptRole.Id) && ids.Contains(addedRole.Id)), default))
            .ReturnsAsync([keptRole, addedRole]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserRoles.Select(ur => ur.RoleId).Should().BeEquivalentTo([keptRole.Id, addedRole.Id]);
        result.Value.Roles.Should().BeEquivalentTo(["Designer", "Gestor de Design"]);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRoleIdsIsEmpty_RemovesAllCurrentRoles()
    {
        // Arrange
        var currentRole = Role.Create("Designer", null);
        var user = CreateExistingUser();
        user.AssignRole(currentRole.Id);

        var command = new UpdateUserRolesCommand(user.Id, []);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserRoles.Should().BeEmpty();
        _roleRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserRolesCommand(userId, [Guid.NewGuid()]);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(userId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSomeRoleIdsDoNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var existingRole = Role.Create("Designer", null);
        var missingRoleId = Guid.NewGuid();
        var user = CreateExistingUser();
        var command = new UpdateUserRolesCommand(user.Id, [existingRole.Id, missingRoleId]);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default)).ReturnsAsync([existingRole]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        user.UserRoles.Should().BeEmpty();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
