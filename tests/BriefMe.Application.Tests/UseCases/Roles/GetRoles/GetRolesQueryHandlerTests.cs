using BriefMe.Application.UseCases.Roles.GetRoles;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Roles.GetRoles;

public class GetRolesQueryHandlerTests
{
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly GetRolesQueryHandler _handler;

    public GetRolesQueryHandlerTests()
    {
        _handler = new GetRolesQueryHandler(_roleRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenRolesExist_ReturnsAllRolesAsListItems()
    {
        // Arrange
        var administrador = Role.Create("Administrador", null, isSystemRole: true);
        var designer = Role.Create("Designer", "Perfil de designer");

        _roleRepositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([administrador, designer]);

        // Act
        var result = await _handler.Handle(new GetRolesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(r => r.RoleId == administrador.Id && r.IsSystemRole);
        result.Value.Should().Contain(r => r.RoleId == designer.Id && r.Description == "Perfil de designer");
    }

    [Fact]
    public async Task Handle_WhenNoRolesExist_ReturnsEmptyList()
    {
        // Arrange
        _roleRepositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new GetRolesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
