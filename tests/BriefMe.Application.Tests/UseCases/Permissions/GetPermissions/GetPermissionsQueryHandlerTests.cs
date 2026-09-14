using BriefMe.Application.UseCases.Permissions.GetPermissions;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Permissions.GetPermissions;

public class GetPermissionsQueryHandlerTests
{
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();
    private readonly GetPermissionsQueryHandler _handler;

    public GetPermissionsQueryHandlerTests()
    {
        _handler = new GetPermissionsQueryHandler(_permissionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenPermissionsExist_ReturnsPermissionsGroupedByGroup()
    {
        // Arrange
        var createUser = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        var viewUser = Permission.Create("users.view", "Ver usuários", null, "Usuários");
        var approveBriefing = Permission.Create("briefings.approve", "Aprovar briefings", null, "Briefings");

        _permissionRepositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([createUser, viewUser, approveBriefing]);

        // Act
        var result = await _handler.Handle(new GetPermissionsQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(g => g.Group == "Usuários" && g.Permissions.Count == 2);
        result.Value.Should().Contain(g => g.Group == "Briefings" && g.Permissions.Count == 1);
    }

    [Fact]
    public async Task Handle_WhenNoPermissionsExist_ReturnsEmptyList()
    {
        // Arrange
        _permissionRepositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new GetPermissionsQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
