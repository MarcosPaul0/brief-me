using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.CreateBriefing;

public class CreateBriefingCommandValidatorTests
{
    private readonly CreateBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenOnlyTitleCategoryAndPriorityProvided_IsValid()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenTitleIsBlank_IsInvalid(string title)
    {
        // Arrange
        var command = new CreateBriefingCommand(title, Priority.Medium, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingCommand.Title));
    }

    [Fact]
    public void Validate_WhenCategoryIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingCommand.CategoryId));
    }

    [Fact]
    public void Validate_WhenPriorityIsNotDefinedEnumValue_IsInvalid()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", (Priority)99, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingCommand.Priority));
    }
}
