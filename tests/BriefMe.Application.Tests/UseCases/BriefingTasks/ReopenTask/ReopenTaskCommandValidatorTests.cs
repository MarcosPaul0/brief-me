using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.ReopenTask;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.ReopenTask;

public class ReopenTaskCommandValidatorTests
{
    private readonly ReopenTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenTaskIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = new ReopenTaskCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.TaskRequired);
    }

    [Fact]
    public void Validate_WhenTaskIdIsFilled_Passes()
    {
        // Arrange
        var command = new ReopenTaskCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
