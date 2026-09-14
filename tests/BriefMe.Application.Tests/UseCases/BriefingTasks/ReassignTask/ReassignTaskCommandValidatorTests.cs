using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.ReassignTask;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.ReassignTask;

public class ReassignTaskCommandValidatorTests
{
    private readonly ReassignTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenTaskAndDesignerAreProvided_IsValid()
    {
        // Arrange
        var command = new ReassignTaskCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTaskIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new ReassignTaskCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.TaskRequired);
    }

    [Fact]
    public void Validate_WhenDesignerIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new ReassignTaskCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.DesignerRequired);
    }

    [Fact]
    public void Validate_WhenBothIdsAreEmpty_ReportsBothFailures()
    {
        // Arrange
        var command = new ReassignTaskCommand(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().HaveCount(2);
    }
}
