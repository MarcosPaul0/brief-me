using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class PermissionRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenCodeExists_ReturnsPermission()
    {
        // Arrange
        await using var context = CreateContext();
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        await context.Permissions.AddAsync(permission);
        await context.SaveChangesAsync();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodeAsync("users.create");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(permission.Id);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenCodeDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodeAsync("inexistente.code");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdsAsync_WhenIdsMatchSubset_ReturnsMatchingPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        var create = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        var update = Permission.Create("users.update", "Editar usuários", null, "Usuários");
        var delete = Permission.Create("users.delete", "Excluir usuários", null, "Usuários");
        await context.Permissions.AddRangeAsync(create, update, delete);
        await context.SaveChangesAsync();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([create.Id, update.Id]);

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Id).Should().BeEquivalentTo([create.Id, update.Id]);
    }

    [Fact]
    public async Task GetAllAsync_WhenPermissionsExist_ReturnsAllPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Permissions.AddRangeAsync(
            Permission.Create("users.create", "Criar usuários", null, "Usuários"),
            Permission.Create("briefings.approve", "Aprovar briefings", null, "Briefings"));
        await context.SaveChangesAsync();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByCodesAsync_WhenCodesMatchSubset_ReturnsOnlyMatchingPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        var create = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        var update = Permission.Create("users.update", "Editar usuários", null, "Usuários");
        var approve = Permission.Create("briefings.approve", "Aprovar briefings", null, "Briefings");
        await context.Permissions.AddRangeAsync(create, update, approve);
        await context.SaveChangesAsync();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodesAsync(["users.create", "briefings.approve"]);

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Code).Should().BeEquivalentTo(["users.create", "briefings.approve"]);
    }

    [Fact]
    public async Task GetByCodesAsync_WhenNoCodesMatch_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Permissions.AddAsync(Permission.Create("users.create", "Criar usuários", null, "Usuários"));
        await context.SaveChangesAsync();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodesAsync(["inexistente.code"]);

        // Assert
        result.Should().BeEmpty();
    }
}
