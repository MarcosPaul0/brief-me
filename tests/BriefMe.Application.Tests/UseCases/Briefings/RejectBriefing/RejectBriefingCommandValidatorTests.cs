using BriefMe.Application.UseCases.Briefings.RejectBriefing;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.RejectBriefing;

public class RejectBriefingCommandValidatorTests
{
    private readonly RejectBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdAndJustificationProvided_IsValid()
    {
        // Arrange
        var command = new RejectBriefingCommand(Guid.NewGuid(), "Out of scope for this quarter.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new RejectBriefingCommand(Guid.Empty, "Out of scope for this quarter.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RejectBriefingCommand.BriefingId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenJustificationIsNullOrWhitespace_IsInvalid(string? justification)
    {
        // Arrange
        var command = new RejectBriefingCommand(Guid.NewGuid(), justification!);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RejectBriefingCommand.Justification));
    }

    [Fact]
    public void Validate_WhenJustificationExceedsMaxLength_IsInvalid()
    {
        // Arrange
        var command = new RejectBriefingCommand(Guid.NewGuid(), new string('a', Justification.MaxLength + 1));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RejectBriefingCommand.Justification));
    }

    [Fact]
    public void Validate_WhenJustificationIsAtMaxLength_IsValid()
    {
        // Arrange
        var command = new RejectBriefingCommand(Guid.NewGuid(), new string('a', Justification.MaxLength));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
