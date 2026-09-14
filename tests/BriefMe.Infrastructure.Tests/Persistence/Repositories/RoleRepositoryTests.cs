using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class RoleRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByNameAsync_WhenNameExists_ReturnsRole()
    {
        // Arrange
        await using var context = CreateContext();
        var role = Role.Create("Designer", null);
        await context.Roles.AddAsync(role);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetByNameAsync("Designer");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(role.Id);
    }

    [Fact]
    public async Task GetByNameAsync_WhenNameDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetByNameAsync("Inexistente");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithPermissionsAsync_WhenRoleExists_ReturnsRoleWithPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        var role = Role.Create("Designer", null);
        role.AddPermission(permission.Id);

        await context.Permissions.AddAsync(permission);
        await context.Roles.AddAsync(role);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetByIdWithPermissionsAsync(role.Id);

        // Assert
        result!.RolePermissions.Single().Permission!.Code.Should().Be("users.create");
    }

    [Fact]
    public async Task GetByIdWithPermissionsAsync_WhenRoleDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetByIdWithPermissionsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdsAsync_WhenIdsMatchSubset_ReturnsMatchingRoles()
    {
        // Arrange
        await using var context = CreateContext();
        var designer = Role.Create("Designer", null);
        var manager = Role.Create("Gestor de Design", null);
        var requester = Role.Create("Solicitante", null);
        await context.Roles.AddRangeAsync(designer, manager, requester);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([designer.Id, manager.Id]);

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.Id).Should().BeEquivalentTo([designer.Id, manager.Id]);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenNoIdsMatch_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([Guid.NewGuid()]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenRolesExist_ReturnsAllRoles()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Roles.AddRangeAsync(
            Role.Create("Designer", null),
            Role.Create("Gerente", null));
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task NameExistsAsync_WhenNameUsedByAnotherRole_ReturnsTrue()
    {
        // Arrange
        await using var context = CreateContext();
        var role = Role.Create("Designer", null);
        await context.Roles.AddAsync(role);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.NameExistsAsync("Designer", ignoreId: Guid.NewGuid());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_WhenIgnoreIdMatchesOwner_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext();
        var role = Role.Create("Designer", null);
        await context.Roles.AddAsync(role);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.NameExistsAsync("Designer", ignoreId: role.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_WhenNameDoesNotExist_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new RoleRepository(context);

        // Act
        var result = await repository.NameExistsAsync("Inexistente");

        // Assert
        result.Should().BeFalse();
    }
}
