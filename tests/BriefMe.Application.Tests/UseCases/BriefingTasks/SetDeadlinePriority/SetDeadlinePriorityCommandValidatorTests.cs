using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.SetDeadlinePriority;

public class SetDeadlinePriorityCommandValidatorTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly SetDeadlinePriorityCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsComplete_IsValid()
    {
        // Arrange
        var command = new SetDeadlinePriorityCommand(Guid.NewGuid(), Today.AddDays(7), Priority.High);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDeadlineIsToday_IsValid()
    {
        // Arrange
        var command = new SetDeadlinePriorityCommand(Guid.NewGuid(), Today, Priority.High);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDeadlineIsInThePast_IsInvalid()
    {
        // Arrange
        var command = new SetDeadlinePriorityCommand(Guid.NewGuid(), Today.AddDays(-1), Priority.High);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.DeadlineInPast);
    }

    [Fact]
    public void Validate_WhenDeadlineIsNotFilled_ReportsItAsRequired()
    {
        // Arrange
        var command = new SetDeadlinePriorityCommand(Guid.NewGuid(), default, Priority.High);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.DeadlineRequired);
    }

    [Fact]
    public void Validate_WhenTaskIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new SetDeadlinePriorityCommand(Guid.Empty, Today.AddDays(7), Priority.High);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.TaskRequired);
    }

    [Fact]
    public void Validate_WhenPriorityIsOutsideTheEnum_IsInvalid()
    {
        // Arrange
        var command = new SetDeadlinePriorityCommand(Guid.NewGuid(), Today.AddDays(7), (Priority)99);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.InvalidPriority);
    }
}
