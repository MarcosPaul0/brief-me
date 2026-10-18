using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class CategoryRepositoryTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(AppDbContext Context, Category Active, Category Inactive)> SeedAsync()
    {
        var context = CreateContext();
        var active = Category.Create("Redes Sociais");
        var inactive = Category.Create("Evento");
        inactive.Deactivate();
        await context.Categories.AddRangeAsync(active, inactive);
        await context.SaveChangesAsync();
        return (context, active, inactive);
    }

    [Fact]
    public async Task GetAllAsync_WhenCategoriesExist_ReturnsAllOrderedByName()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();
        await using var _ = context;

        // Act
        var result = await new CategoryRepository(context).GetAllAsync();

        // Assert
        result.Select(c => c.Name).Should().Equal("Evento", "Redes Sociais");
    }

    [Fact]
    public async Task GetActiveAsync_WhenSomeAreInactive_ReturnsOnlyActive()
    {
        // Arrange
        var (context, active, _) = await SeedAsync();
        await using var _ = context;

        // Act
        var result = await new CategoryRepository(context).GetActiveAsync();

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task ExistsActiveAsync_WhenCategoryIsInactive_ReturnsFalse()
    {
        // Arrange
        var (context, _, inactive) = await SeedAsync();
        await using var _ = context;

        // Act
        var result = await new CategoryRepository(context).ExistsActiveAsync(inactive.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsActiveAsync_WhenCategoryIsActive_ReturnsTrue()
    {
        // Arrange
        var (context, active, _) = await SeedAsync();
        await using var _ = context;

        // Act
        var result = await new CategoryRepository(context).ExistsActiveAsync(active.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("evento")]
    [InlineData("  EVENTO ")]
    public async Task NameExistsAsync_WhenNameDiffersOnlyByCaseOrSpaces_ReturnsTrue(string name)
    {
        // Arrange
        var (context, _, _) = await SeedAsync();
        await using var _ = context;

        // Act
        var result = await new CategoryRepository(context).NameExistsAsync(name);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_WhenTheOnlyMatchIsIgnored_ReturnsFalse()
    {
        // Arrange
        var (context, _, inactive) = await SeedAsync();
        await using var _ = context;

        // Act
        var result = await new CategoryRepository(context).NameExistsAsync("Evento", inactive.Id);

        // Assert
        result.Should().BeFalse();
    }
}
