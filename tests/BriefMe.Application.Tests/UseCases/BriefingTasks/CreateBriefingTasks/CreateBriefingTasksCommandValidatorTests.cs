using BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.CreateBriefingTasks;

public class CreateBriefingTasksCommandValidatorTests
{
    private readonly CreateBriefingTasksCommandValidator _validator = new();

    private static CreateBriefingTasksCommand CommandWith(params CreateBriefingTaskItem[] items) =>
        new(Guid.NewGuid(), items);

    [Fact]
    public void Validate_WhenBriefingAndOneTaskProvided_IsValid()
    {
        // Arrange
        var command = CommandWith(new CreateBriefingTaskItem("Key visual", Priority.Medium));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBatchHasSeveralValidTasks_IsValid()
    {
        // Arrange
        var command = CommandWith(
            new CreateBriefingTaskItem("Key visual", Priority.High),
            new CreateBriefingTaskItem("Story assets", Priority.Low));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new CreateBriefingTasksCommand(
            Guid.Empty,
            [new CreateBriefingTaskItem("Key visual", Priority.Medium)]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingTasksCommand.BriefingId));
    }

    [Fact]
    public void Validate_WhenTaskListIsEmpty_IsInvalid()
    {
        // Arrange
        var command = CommandWith();

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingTasksCommand.Tasks));
    }

    [Fact]
    public void Validate_WhenTaskListIsNull_IsInvalid()
    {
        // Arrange
        var command = new CreateBriefingTasksCommand(Guid.NewGuid(), null!);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenATaskTitleIsBlank_IsInvalid(string title)
    {
        // Arrange
        var command = CommandWith(new CreateBriefingTaskItem(title, Priority.Medium));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenATaskTitleIsBlank_IdentifiesTheOffendingItemByIndex()
    {
        // Arrange
        var command = CommandWith(
            new CreateBriefingTaskItem("Key visual", Priority.High),
            new CreateBriefingTaskItem("", Priority.Low));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().Contain(e => e.PropertyName == "Tasks[1].Title");
    }

    [Fact]
    public void Validate_WhenATaskTitleExceedsTheColumnLength_IsInvalid()
    {
        // Arrange
        var command = CommandWith(new CreateBriefingTaskItem(new string('a', 201), Priority.Medium));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenATaskTitleIsExactlyTheColumnLength_IsValid()
    {
        // Arrange
        var command = CommandWith(new CreateBriefingTaskItem(new string('a', 200), Priority.Medium));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenATaskPriorityIsNotAnEnumValue_IsInvalid()
    {
        // Arrange
        var command = CommandWith(new CreateBriefingTaskItem("Key visual", (Priority)99));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
