using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace BriefMe.Infrastructure.Tests.Persistence;

public class SeededCategoryRepositoryTests
{
    private readonly SeededCategoryRepository _repository = new();

    [Fact]
    public async Task GetActiveAsync_ReturnsSeededCatalogOrderedByName()
    {
        // Act
        var categories = await _repository.GetActiveAsync();

        // Assert
        categories.Should().NotBeEmpty();
        categories.Select(c => c.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ExistsActiveAsync_WhenIdIsInCatalog_ReturnsTrue()
    {
        // Arrange
        var known = (await _repository.GetActiveAsync())[0];

        // Act
        var exists = await _repository.ExistsActiveAsync(known.Id);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsActiveAsync_WhenIdIsUnknown_ReturnsFalse()
    {
        // Act
        var exists = await _repository.ExistsActiveAsync(Guid.NewGuid());

        // Assert
        exists.Should().BeFalse();
    }
}
