using BriefMe.Application.UseCases.Roles.UpdateRole;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Roles.UpdateRole;

public class UpdateRoleCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateRoleCommandHandler _handler;

    public UpdateRoleCommandHandlerTests()
    {
        _handler = new UpdateRoleCommandHandler(
            _roleRepositoryMock.Object,
            _permissionRepositoryMock.Object,
            _unitOfWorkMock.Object);

        _roleRepositoryMock
            .Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), default))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_WhenDataIsValid_UpdatesNameAndDescription()
    {
        // Arrange
        var role = Role.Create("Designer", "Descrição antiga");
        var command = new UpdateRoleCommand(role.Id, "Designer Sênior", "Descrição nova");

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Designer Sênior");
        result.Value.Description.Should().Be("Descrição nova");
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRoleDoesNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var command = new UpdateRoleCommand(roleId, "Designer Sênior", null);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(roleId, default)).ReturnsAsync((Role?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNewNameAlreadyUsedByAnotherRole_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var command = new UpdateRoleCommand(role.Id, "Gestor de Design", null);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);
        _roleRepositoryMock.Setup(r => r.NameExistsAsync("Gestor de Design", role.Id, default)).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRoleHasPermissions_ReturnsUpdatedRoleWithPermissionCodes()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var permission = Permission.Create("briefings.view", "Ver briefings", null, "Briefings");
        role.AddPermission(permission.Id);
        var command = new UpdateRoleCommand(role.Id, "Designer Sênior", "Nova descrição");

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);
        _permissionRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(permission.Id)), default))
            .ReturnsAsync([permission]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().ContainSingle().Which.Should().Be("briefings.view");
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }
}
