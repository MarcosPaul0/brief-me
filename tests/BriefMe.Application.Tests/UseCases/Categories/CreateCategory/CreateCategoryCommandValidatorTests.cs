using BriefMe.Application.UseCases.Categories.CreateCategory;
using BriefMe.Domain.Entities;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Categories.CreateCategory;

public class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenNameIsBlank_HasErrorForName(string name)
    {
        // Arrange
        var command = new CreateCategoryCommand(name);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenNameIsTooLong_HasErrorForName()
    {
        // Arrange
        var command = new CreateCategoryCommand(new string('a', Category.NameMaxLength + 1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenNameIsValid_HasNoErrors()
    {
        // Arrange
        var command = new CreateCategoryCommand("Vídeo");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
