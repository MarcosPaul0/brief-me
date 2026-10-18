using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Seeding;

public class CategorySeederTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CategorySeeder CreateSeeder(AppDbContext context) =>
        new(new CategoryRepository(context), new UnitOfWork(context));

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsFresh_CreatesTheFiveDefaultsActive()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        await CreateSeeder(context).SeedAsync();

        // Assert
        var categories = await context.Categories.ToListAsync();
        categories.Should().HaveCount(5).And.OnlyContain(c => c.IsActive);
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_DoesNotDuplicate()
    {
        // Arrange
        await using var context = CreateContext();
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        (await context.Categories.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task SeedAsync_WhenAdminDeactivatedADefault_KeepsItInactive()
    {
        // Arrange
        await using var context = CreateContext();
        var evento = Category.Create("Evento");
        evento.Deactivate();
        await context.Categories.AddAsync(evento);
        await context.SaveChangesAsync();

        // Act
        await CreateSeeder(context).SeedAsync();

        // Assert
        (await context.Categories.SingleAsync(c => c.Name == "Evento")).IsActive.Should().BeFalse();
    }
}
