using BriefMe.Application.UseCases.Roles.UpdateRolePermissions;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Roles.UpdateRolePermissions;

public class UpdateRolePermissionsCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateRolePermissionsCommandHandler _handler;

    public UpdateRolePermissionsCommandHandlerTests()
    {
        _handler = new UpdateRolePermissionsCommandHandler(
            _roleRepositoryMock.Object,
            _permissionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenPermissionIdsExist_ReplacesCurrentPermissionsWithTargetSet()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var keptPermission = Permission.Create("briefings.view", "Ver briefings", null, "Briefings");
        var removedPermission = Permission.Create("users.view", "Ver usuários", null, "Usuários");
        var addedPermission = Permission.Create("briefings.approve", "Aprovar briefings", null, "Briefings");
        role.AddPermission(keptPermission.Id);
        role.AddPermission(removedPermission.Id);

        var command = new UpdateRolePermissionsCommand(role.Id, [keptPermission.Id, addedPermission.Id]);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);
        _permissionRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(keptPermission.Id) && ids.Contains(addedPermission.Id)), default))
            .ReturnsAsync([keptPermission, addedPermission]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        role.RolePermissions.Select(rp => rp.PermissionId).Should().BeEquivalentTo([keptPermission.Id, addedPermission.Id]);
        result.Value.PermissionCodes.Should().BeEquivalentTo(["briefings.view", "briefings.approve"]);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPermissionIdsIsEmpty_RemovesAllCurrentPermissions()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var currentPermission = Permission.Create("briefings.view", "Ver briefings", null, "Briefings");
        role.AddPermission(currentPermission.Id);

        var command = new UpdateRolePermissionsCommand(role.Id, []);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        role.RolePermissions.Should().BeEmpty();
        _permissionRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRoleDoesNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var command = new UpdateRolePermissionsCommand(roleId, [Guid.NewGuid()]);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(roleId, default)).ReturnsAsync((Role?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSomePermissionIdsDoNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var existingPermission = Permission.Create("briefings.view", "Ver briefings", null, "Briefings");
        var missingPermissionId = Guid.NewGuid();
        var command = new UpdateRolePermissionsCommand(role.Id, [existingPermission.Id, missingPermissionId]);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);
        _permissionRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default)).ReturnsAsync([existingPermission]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        role.RolePermissions.Should().BeEmpty();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
