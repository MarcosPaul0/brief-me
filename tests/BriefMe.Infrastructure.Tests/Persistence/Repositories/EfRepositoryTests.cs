using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class EfRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityExists_ReturnsEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<Permission>(context);
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        await repository.AddAsync(permission);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(permission.Id);

        // Assert
        result.Should().Be(permission);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<Permission>(context);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_WhenCommitted_PersistsEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<Permission>(context);
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");

        // Act
        await repository.AddAsync(permission);
        await context.SaveChangesAsync();

        // Assert
        (await context.Permissions.FindAsync(permission.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Update_WhenCommitted_PersistsChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<Role>(context);
        var role = Role.Create("Designer", null);
        await repository.AddAsync(role);
        await context.SaveChangesAsync();

        // Act
        role.Rename("Designer Sênior");
        repository.Update(role);
        await context.SaveChangesAsync();

        // Assert
        var persisted = await context.Roles.FindAsync(role.Id);
        persisted!.Name.Should().Be("Designer Sênior");
    }

    [Fact]
    public async Task Remove_WhenCommitted_DeletesEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<Permission>(context);
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        await repository.AddAsync(permission);
        await context.SaveChangesAsync();

        // Act
        repository.Remove(permission);
        await context.SaveChangesAsync();

        // Assert
        (await context.Permissions.FindAsync(permission.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Query_WhenEntityIsSoftDeleted_ExcludesItByDefault()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<Role>(context);
        var role = Role.Create("Designer", null);
        await repository.AddAsync(role);
        await context.SaveChangesAsync();
        role.MarkAsDeleted("admin@briefme.com");
        repository.Update(role);
        await context.SaveChangesAsync();

        // Act
        var result = repository.Query().ToList();

        // Assert
        result.Should().BeEmpty();
    }
}
