using BriefMe.Application.UseCases.Roles.GetRoleById;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Roles.GetRoleById;

public class GetRoleByIdQueryHandlerTests
{
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();
    private readonly GetRoleByIdQueryHandler _handler;

    public GetRoleByIdQueryHandlerTests()
    {
        _handler = new GetRoleByIdQueryHandler(_roleRepositoryMock.Object, _permissionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenRoleExists_ReturnsRoleResultWithPermissionCodes()
    {
        // Arrange
        var role = Role.Create("Designer", "Perfil de designer");
        var permission = Permission.Create("briefings.view", "Ver briefings", null, "Briefings");
        role.AddPermission(permission.Id);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);
        _permissionRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(permission.Id)), default))
            .ReturnsAsync([permission]);

        // Act
        var result = await _handler.Handle(new GetRoleByIdQuery(role.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Designer");
        result.Value.PermissionCodes.Should().ContainSingle().Which.Should().Be("briefings.view");
    }

    [Fact]
    public async Task Handle_WhenRoleDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(roleId, default)).ReturnsAsync((Role?)null);

        // Act
        var result = await _handler.Handle(new GetRoleByIdQuery(roleId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenRoleHasNoPermissions_ReturnsEmptyPermissionCodesWithoutQueryingPermissions()
    {
        // Arrange
        var role = Role.Create("Solicitante", null);

        _roleRepositoryMock.Setup(r => r.GetByIdWithPermissionsAsync(role.Id, default)).ReturnsAsync(role);

        // Act
        var result = await _handler.Handle(new GetRoleByIdQuery(role.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEmpty();
        _permissionRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }
}
