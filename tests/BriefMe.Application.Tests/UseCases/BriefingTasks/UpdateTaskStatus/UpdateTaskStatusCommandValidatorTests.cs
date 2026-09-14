using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.UpdateTaskStatus;

public class UpdateTaskStatusCommandValidatorTests
{
    private readonly UpdateTaskStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenTaskAndStatusAreProvided_IsValid()
    {
        // Arrange
        var command = new UpdateTaskStatusCommand(Guid.NewGuid(), BriefingTaskStatus.InProgress);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTaskIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new UpdateTaskStatusCommand(Guid.Empty, BriefingTaskStatus.InProgress);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.TaskRequired);
    }

    [Fact]
    public void Validate_WhenStatusIsNotFilled_IsInvalid()
    {
        // Arrange
        var command = new UpdateTaskStatusCommand(Guid.NewGuid(), default);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.InvalidStatus);
    }

    [Fact]
    public void Validate_WhenStatusIsOutsideTheEnum_IsInvalid()
    {
        // Arrange
        var command = new UpdateTaskStatusCommand(Guid.NewGuid(), (BriefingTaskStatus)99);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.InvalidStatus);
    }

    [Fact]
    public void Validate_WhenTaskIdAndStatusAreInvalid_ReportsBothFailures()
    {
        // Arrange
        var command = new UpdateTaskStatusCommand(Guid.Empty, default);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().HaveCount(2);
    }
}
