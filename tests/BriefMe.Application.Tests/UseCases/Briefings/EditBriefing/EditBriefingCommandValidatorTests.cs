using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.EditBriefing;

public class EditBriefingCommandValidatorTests
{
    private readonly EditBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdTitleAndPriorityProvided_IsValid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", Priority.Medium);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.Empty, "New title", Priority.Medium);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EditBriefingCommand.BriefingId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenTitleIsBlank_IsInvalid(string title)
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), title, Priority.Medium);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EditBriefingCommand.Title));
    }

    [Fact]
    public void Validate_WhenPriorityIsNotDefinedEnumValue_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", (Priority)99);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EditBriefingCommand.Priority));
    }
}
