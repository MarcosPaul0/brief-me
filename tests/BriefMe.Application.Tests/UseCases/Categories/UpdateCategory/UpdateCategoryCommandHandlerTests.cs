using BriefMe.Application.UseCases.Categories.UpdateCategory;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Categories.UpdateCategory;

public class UpdateCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateCategoryCommandHandler _handler;

    public UpdateCategoryCommandHandlerTests()
    {
        _handler = new UpdateCategoryCommandHandler(_categoryRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCategoryExistsAndNameIsFree_RenamesAndCommits()
    {
        // Arrange
        var category = Category.Create("Evento");
        _categoryRepositoryMock.Setup(r => r.GetByIdAsync(category.Id, default)).ReturnsAsync(category);

        // Act
        var result = await _handler.Handle(new UpdateCategoryCommand(category.Id, "Eventos", "Feiras"), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Name = "Eventos", Description = "Feiras" });
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _categoryRepositoryMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Category?)null);

        // Act
        var result = await _handler.Handle(new UpdateCategoryCommand(id, "Eventos"), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAnotherCategoryHasTheName_ReturnsConflictWithoutCommitting()
    {
        // Arrange
        var category = Category.Create("Evento");
        _categoryRepositoryMock.Setup(r => r.GetByIdAsync(category.Id, default)).ReturnsAsync(category);
        _categoryRepositoryMock.Setup(r => r.NameExistsAsync("Redes Sociais", category.Id, default)).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(new UpdateCategoryCommand(category.Id, "Redes Sociais"), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
