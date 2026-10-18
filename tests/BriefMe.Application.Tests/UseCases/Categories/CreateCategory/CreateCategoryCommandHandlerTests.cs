using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Categories.CreateCategory;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Categories.CreateCategory;

public class CreateCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateCategoryCommandHandler _handler;

    public CreateCategoryCommandHandlerTests()
    {
        _handler = new CreateCategoryCommandHandler(_categoryRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNameIsFree_ReturnsActiveCategory()
    {
        // Arrange
        var command = new CreateCategoryCommand("Vídeo", "Animações e vídeos curtos");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Name = "Vídeo", Description = "Animações e vídeos curtos", IsActive = true });
    }

    [Fact]
    public async Task Handle_WhenNameIsFree_PersistsTheCategory()
    {
        // Arrange
        var command = new CreateCategoryCommand("Vídeo");

        // Act
        await _handler.Handle(command, default);

        // Assert
        _categoryRepositoryMock.Verify(r => r.AddAsync(It.Is<Category>(c => c.Name == "Vídeo"), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ReturnsConflictWithoutPersisting()
    {
        // Arrange
        _categoryRepositoryMock.Setup(r => r.NameExistsAsync("Evento", null, default)).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(new CreateCategoryCommand("Evento"), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(CategoryErrors.DuplicateName);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
