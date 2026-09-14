using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.CancelBriefing;

public class CancelBriefingCommandValidatorTests
{
    private readonly CancelBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdProvided_IsValid()
    {
        // Arrange
        var command = new CancelBriefingCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new CancelBriefingCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelBriefingCommand.BriefingId));
    }
}