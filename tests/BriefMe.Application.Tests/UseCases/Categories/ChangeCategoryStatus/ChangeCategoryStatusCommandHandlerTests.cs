using BriefMe.Application.UseCases.Categories.ChangeCategoryStatus;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Categories.ChangeCategoryStatus;

public class ChangeCategoryStatusCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ChangeCategoryStatusCommandHandler _handler;

    public ChangeCategoryStatusCommandHandlerTests()
    {
        _handler = new ChangeCategoryStatusCommandHandler(_categoryRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WhenCategoryExists_AppliesTheRequestedStatus(bool isActive)
    {
        // Arrange
        var category = Category.Create("Evento");
        if (isActive)
            category.Deactivate();
        _categoryRepositoryMock.Setup(r => r.GetByIdAsync(category.Id, default)).ReturnsAsync(category);

        // Act
        var result = await _handler.Handle(new ChangeCategoryStatusCommand(category.Id, isActive), default);

        // Assert
        result.Value.IsActive.Should().Be(isActive);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ReturnsNotFoundWithoutCommitting()
    {
        // Arrange
        var id = Guid.NewGuid();
        _categoryRepositoryMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Category?)null);

        // Act
        var result = await _handler.Handle(new ChangeCategoryStatusCommand(id, false), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
