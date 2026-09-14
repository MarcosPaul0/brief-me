using BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.RequestChangesBriefing;

public class RequestChangesBriefingCommandValidatorTests
{
    private readonly RequestChangesBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdAndJustificationProvided_IsValid()
    {
        // Arrange
        var command = new RequestChangesBriefingCommand(Guid.NewGuid(), "Add the target audience section.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new RequestChangesBriefingCommand(Guid.Empty, "Add the target audience section.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RequestChangesBriefingCommand.BriefingId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenJustificationIsNullOrWhitespace_IsInvalid(string? justification)
    {
        // Arrange
        var command = new RequestChangesBriefingCommand(Guid.NewGuid(), justification!);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RequestChangesBriefingCommand.Justification));
    }

    [Fact]
    public void Validate_WhenJustificationExceedsMaxLength_IsInvalid()
    {
        // Arrange
        var command = new RequestChangesBriefingCommand(
            Guid.NewGuid(),
            new string('a', Justification.MaxLength + 1));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RequestChangesBriefingCommand.Justification));
    }

    [Fact]
    public void Validate_WhenJustificationIsAtMaxLength_IsValid()
    {
        // Arrange
        var command = new RequestChangesBriefingCommand(
            Guid.NewGuid(),
            new string('a', Justification.MaxLength));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
