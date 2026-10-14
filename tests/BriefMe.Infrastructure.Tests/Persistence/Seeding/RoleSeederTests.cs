using BriefMe.Domain.Constants;
using BriefMe.Domain.Exceptions;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Seeding;

public class RoleSeederTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RoleSeeder CreateSeeder(AppDbContext context)
    {
        var unitOfWork = new UnitOfWork(context);
        return new RoleSeeder(new RoleRepository(context), new PermissionRepository(context), unitOfWork);
    }

    private static async Task SeedPermissionsAsync(AppDbContext context)
    {
        var seeder = new PermissionSeeder(
            new PermissionRepository(context),
            new UnitOfWork(context));
        await seeder.SeedAsync();
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseHasCatalog_CreatesTheFourSystemRoles()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var roles = await context.Roles.ToListAsync();
        roles.Select(role => role.Name).Should().BeEquivalentTo(Roles.All);
        roles.Should().OnlyContain(role => role.IsSystemRole);
    }

    [Fact]
    public async Task SeedAsync_ForAdministrador_AssignsEveryCatalogPermission()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var administrador = await context.Roles
            .Include(role => role.RolePermissions)
            .SingleAsync(role => role.Name == Roles.Administrador);
        administrador.RolePermissions.Should().HaveCount(Permissions.Catalog.Count);
    }

    [Fact]
    public async Task SeedAsync_ForDesigner_AssignsExactlyItsDefinedPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var designer = await context.Roles
            .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleAsync(role => role.Name == Roles.Designer);
        var expected = Roles.Catalog.Single(definition => definition.Name == Roles.Designer).PermissionCodes;
        designer.RolePermissions.Select(rolePermission => rolePermission.Permission!.Code)
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_DoesNotDuplicateRolesOrPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        var roleCount = await context.Roles.CountAsync();
        var rolePermissionCount = await context.Set<Domain.Entities.RolePermission>().CountAsync();
        var expectedAssociations = Roles.Catalog.Sum(definition => definition.PermissionCodes.Count);
        roleCount.Should().Be(Roles.All.Count);
        rolePermissionCount.Should().Be(expectedAssociations);
    }

    [Fact]
    public async Task SeedAsync_WhenAReferencedPermissionIsMissing_ThrowsClearError()
    {
        // Arrange
        await using var context = CreateContext();
        // Catalog intentionally NOT seeded, so every referenced code is absent.
        var seeder = CreateSeeder(context);

        // Act
        var act = () => seeder.SeedAsync();

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*ausentes no catálogo*");
    }
}
