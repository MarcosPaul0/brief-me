using BriefMe.Application.UseCases.Categories.GetActiveCategories;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Categories.GetActiveCategories;

public class GetActiveCategoriesQueryHandlerTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly GetActiveCategoriesQueryHandler _handler;

    public GetActiveCategoriesQueryHandlerTests()
    {
        _handler = new GetActiveCategoriesQueryHandler(_categoryRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCategoriesExist_MapsToDtos()
    {
        // Arrange
        var first = new CategoryInfo(Guid.NewGuid(), "Campanha");
        var second = new CategoryInfo(Guid.NewGuid(), "Evento");
        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([first, second]);

        // Act
        var result = await _handler.Handle(new GetActiveCategoriesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(first.Id);
        result.Value[0].Name.Should().Be("Campanha");
        result.Value[1].Name.Should().Be("Evento");
    }

    [Fact]
    public async Task Handle_WhenNoCategories_ReturnsEmptyList()
    {
        // Arrange
        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new GetActiveCategoriesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
