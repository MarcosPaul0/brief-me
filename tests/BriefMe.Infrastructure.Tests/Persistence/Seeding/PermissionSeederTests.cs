using BriefMe.Domain.Constants;
using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Seeding;

public class PermissionSeederTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PermissionSeeder CreateSeeder(AppDbContext context)
    {
        var repository = new PermissionRepository(context);
        var unitOfWork = new UnitOfWork(context);
        return new PermissionSeeder(repository, unitOfWork);
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsEmpty_PersistsEntireCatalog()
    {
        // Arrange
        await using var context = CreateContext();
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var storedCodes = await context.Permissions.Select(p => p.Code).ToListAsync();
        storedCodes.Should().BeEquivalentTo(Permissions.Catalog.Select(d => d.Code));
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_DoesNotDuplicatePermissions()
    {
        // Arrange
        await using var context = CreateContext();
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        var count = await context.Permissions.CountAsync();
        count.Should().Be(Permissions.Catalog.Count);
    }

    [Fact]
    public async Task SeedAsync_WhenCatalogPartiallyPresent_InsertsOnlyMissingAndKeepsExisting()
    {
        // Arrange
        await using var context = CreateContext();
        var existing = Permission.Create(Permissions.Users.View, "Preexistente", null, Permissions.Groups.Users);
        await context.Permissions.AddAsync(existing);
        await context.SaveChangesAsync();
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var count = await context.Permissions.CountAsync();
        count.Should().Be(Permissions.Catalog.Count);
        var preserved = await context.Permissions.SingleAsync(p => p.Code == Permissions.Users.View);
        preserved.Id.Should().Be(existing.Id);
    }
}
