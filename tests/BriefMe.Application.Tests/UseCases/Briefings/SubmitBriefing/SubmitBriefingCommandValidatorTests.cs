using BriefMe.Application.UseCases.Briefings.SubmitBriefing;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.SubmitBriefing;

public class SubmitBriefingCommandValidatorTests
{
    private readonly SubmitBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdProvided_IsValid()
    {
        // Arrange
        var command = new SubmitBriefingCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new SubmitBriefingCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitBriefingCommand.BriefingId));
    }
}
