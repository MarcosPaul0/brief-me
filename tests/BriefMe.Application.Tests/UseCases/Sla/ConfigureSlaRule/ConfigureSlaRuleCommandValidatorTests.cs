using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Sla.ConfigureSlaRule;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Sla.ConfigureSlaRule;

public class ConfigureSlaRuleCommandValidatorTests
{
    private readonly ConfigureSlaRuleCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(SlaDeadline.MaxHours + 1)]
    public void Validate_WhenDeadlineIsOutsideTheAcceptedRange_ReturnsValidationFailure(int deadlineHours)
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(Guid.NewGuid(), Priority.Urgent, deadlineHours);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(SlaDeadline.MinHours)]
    [InlineData(SlaDeadline.MaxHours)]
    public void Validate_WhenDeadlineIsAtTheBoundary_Passes(int deadlineHours)
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(Guid.NewGuid(), Priority.Urgent, deadlineHours);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenPriorityIsOutsideTheEnum_ReturnsValidationFailure()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(Guid.NewGuid(), (Priority)99, 24);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(SlaErrors.Validation.InvalidPriority);
    }

    [Fact]
    public void Validate_WhenScopeSidesAreAbsent_Passes()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(null, null, 240);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
