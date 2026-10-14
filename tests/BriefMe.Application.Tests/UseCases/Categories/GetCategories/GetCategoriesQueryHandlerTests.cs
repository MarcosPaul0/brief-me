using BriefMe.Application.UseCases.Categories.GetCategories;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Categories.GetCategories;

public class GetCategoriesQueryHandlerTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly GetCategoriesQueryHandler _handler;

    public GetCategoriesQueryHandlerTests()
    {
        _handler = new GetCategoriesQueryHandler(_categoryRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCategoriesExist_ReturnsActiveAndInactiveInRepositoryOrder()
    {
        // Arrange
        var active = Category.Create("Campanha");
        var inactive = Category.Create("Evento");
        inactive.Deactivate();
        _categoryRepositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([active, inactive]);

        // Act
        var result = await _handler.Handle(new GetCategoriesQuery(), default);

        // Assert
        result.Value.Select(c => (c.Name, c.IsActive)).Should().Equal(("Campanha", true), ("Evento", false));
    }
}
