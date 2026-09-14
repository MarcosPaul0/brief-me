using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Services;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Services;

public class BriefingProgressCalculatorTests
{
    private readonly BriefingProgressCalculator _calculator = new();

    private static BriefingTask NewTask() =>
        BriefingTask.Create(Guid.NewGuid(), "Design the key visual", Priority.Medium);

    /// <summary>
    /// Walks a task to <paramref name="status"/> through legal transitions only — every RF020 state is
    /// reachable from NotStarted, so no reflection is needed to arrange one.
    /// </summary>
    private static BriefingTask TaskIn(BriefingTaskStatus status)
    {
        var task = NewTask();

        switch (status)
        {
            case BriefingTaskStatus.NotStarted:
                break;
            case BriefingTaskStatus.InProgress:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                break;
            case BriefingTaskStatus.Blocked:
                task.UpdateStatus(BriefingTaskStatus.Blocked);
                break;
            case BriefingTaskStatus.AwaitingTechnicalApproval:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                break;
            case BriefingTaskStatus.Rejected:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                task.UpdateStatus(BriefingTaskStatus.Rejected);
                break;
            case BriefingTaskStatus.AwaitingRequesterApproval:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                task.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
                break;
            case BriefingTaskStatus.Approved:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                task.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
                task.UpdateStatus(BriefingTaskStatus.Approved);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped status.");
        }

        return task;
    }

    [Fact]
    public void Calculate_WhenHalfOfTheTasksAreApproved_ReturnsFiftyPercent()
    {
        // Arrange — the literal acceptance criterion of BRF-035.
        BriefingTask[] tasks =
        [
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.InProgress),
            TaskIn(BriefingTaskStatus.NotStarted)
        ];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.Percentage.Should().Be(50m);
    }

    [Fact]
    public void Calculate_WhenTasksAreGiven_ReturnsTotalAndApprovedCounters()
    {
        // Arrange
        BriefingTask[] tasks =
        [
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.InProgress),
            TaskIn(BriefingTaskStatus.NotStarted)
        ];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.Should().BeEquivalentTo(new { TotalTasks = 4, ApprovedTasks = 2 });
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void Calculate_WhenTaskIsNotApproved_CountsItInTheTotalOnly(BriefingTaskStatus status)
    {
        // Arrange
        BriefingTask[] tasks = [TaskIn(status)];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.Should().BeEquivalentTo(new { TotalTasks = 1, ApprovedTasks = 0, Percentage = 0m });
    }

    [Fact]
    public void Calculate_WhenEveryTaskIsApproved_ReturnsHundredPercent()
    {
        // Arrange
        BriefingTask[] tasks =
        [
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.Approved)
        ];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.Percentage.Should().Be(100m);
    }

    [Fact]
    public void Calculate_WhenEveryTaskIsApproved_ReturnsComplete()
    {
        // Arrange
        BriefingTask[] tasks = [TaskIn(BriefingTaskStatus.Approved), TaskIn(BriefingTaskStatus.Approved)];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Calculate_WhenOneTaskIsStillPending_ReturnsNotComplete()
    {
        // Arrange
        BriefingTask[] tasks =
        [
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.AwaitingRequesterApproval)
        ];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Calculate_WhenCalledTwiceWithTheSameTasks_ReturnsTheSameResult()
    {
        // Arrange — the service is pure: no state, no clock, no repository.
        BriefingTask[] tasks =
        [
            TaskIn(BriefingTaskStatus.Approved),
            TaskIn(BriefingTaskStatus.Blocked),
            TaskIn(BriefingTaskStatus.InProgress)
        ];

        // Act
        var first = _calculator.Calculate(tasks);
        var second = _calculator.Calculate(tasks);

        // Assert
        second.Should().Be(first);
    }

    [Fact]
    public void Calculate_WhenThereAreNoTasks_ReturnsZeroPercent()
    {
        // Arrange — a briefing approved but not broken down yet (BRF-026).
        BriefingTask[] tasks = [];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.Should().BeEquivalentTo(new { TotalTasks = 0, ApprovedTasks = 0, Percentage = 0m });
    }

    [Fact]
    public void Calculate_WhenThereAreNoTasks_ReturnsNotComplete()
    {
        // Arrange
        BriefingTask[] tasks = [];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Calculate_WhenTheSameTaskAppearsTwice_CountsItTwice()
    {
        // Arrange — deduplicating here would hide a caller bug; the repository returns distinct entities.
        var task = TaskIn(BriefingTaskStatus.Approved);
        BriefingTask[] tasks = [task, task];

        // Act
        var progress = _calculator.Calculate(tasks);

        // Assert
        progress.TotalTasks.Should().Be(2);
    }

    [Fact]
    public void Calculate_WhenTasksAreNull_ThrowsArgumentNullException()
    {
        // Arrange
        // (none needed)

        // Act
        var act = () => _calculator.Calculate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
