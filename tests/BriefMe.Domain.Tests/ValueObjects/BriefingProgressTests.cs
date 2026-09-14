using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class BriefingProgressTests
{
    [Theory]
    [InlineData(4, 2, 50)]
    [InlineData(3, 1, 33.33)]
    [InlineData(3, 2, 66.67)]
    [InlineData(8, 3, 37.5)]
    public void FromCounts_WhenSomeTasksAreApproved_ReturnsRatioRoundedToTwoDecimals(
        int totalTasks,
        int approvedTasks,
        decimal expectedPercentage)
    {
        // Arrange
        // (none needed)

        // Act
        var progress = BriefingProgress.FromCounts(totalTasks, approvedTasks);

        // Assert
        progress.Percentage.Should().Be(expectedPercentage);
    }

    [Fact]
    public void FromCounts_WhenOnlyOneTaskIsPending_ReturnsLessThanHundredPercent()
    {
        // Arrange — 199 of 200 rounds to 100; a screen must not claim the briefing is finished.

        // Act
        var progress = BriefingProgress.FromCounts(200, 199);

        // Assert
        progress.Percentage.Should().BeLessThan(100m);
    }

    [Fact]
    public void FromCounts_WhenOnlyOneTaskIsPending_ReturnsNotComplete()
    {
        // Arrange
        // (none needed)

        // Act
        var progress = BriefingProgress.FromCounts(200, 199);

        // Assert
        progress.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void FromCounts_WhenASingleTaskOfManyIsApproved_ReturnsMoreThanZeroPercent()
    {
        // Arrange — 1 of 10000 rounds to 0; work already delivered must not read as untouched.

        // Act
        var progress = BriefingProgress.FromCounts(10000, 1);

        // Assert
        progress.Percentage.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void FromCounts_WhenEveryTaskIsApproved_ReturnsHundredPercent()
    {
        // Arrange
        // (none needed)

        // Act
        var progress = BriefingProgress.FromCounts(7, 7);

        // Assert
        progress.Percentage.Should().Be(100m);
    }

    [Fact]
    public void FromCounts_WhenEveryTaskIsApproved_ReturnsComplete()
    {
        // Arrange
        // (none needed)

        // Act
        var progress = BriefingProgress.FromCounts(7, 7);

        // Assert
        progress.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void FromCounts_WhenThereAreNoTasks_ReturnsZeroPercent()
    {
        // Arrange
        // (none needed)

        // Act
        var progress = BriefingProgress.FromCounts(0, 0);

        // Assert
        progress.Percentage.Should().Be(0m);
    }

    [Fact]
    public void FromCounts_WhenThereAreNoTasks_ReturnsNotComplete()
    {
        // Arrange — 0 of 0 is a briefing not broken down yet, never a finished one (RF058).

        // Act
        var progress = BriefingProgress.FromCounts(0, 0);

        // Assert
        progress.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void FromCounts_WhenNoTaskIsApproved_ReturnsZeroPercent()
    {
        // Arrange
        // (none needed)

        // Act
        var progress = BriefingProgress.FromCounts(5, 0);

        // Assert
        progress.Percentage.Should().Be(0m);
    }

    [Fact]
    public void FromCounts_WhenApprovedExceedsTotal_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        // (none needed)

        // Act
        var act = () => BriefingProgress.FromCounts(2, 3);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    public void FromCounts_WhenACountIsNegative_ThrowsArgumentOutOfRangeException(
        int totalTasks,
        int approvedTasks)
    {
        // Arrange
        // (none needed)

        // Act
        var act = () => BriefingProgress.FromCounts(totalTasks, approvedTasks);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromCounts_WhenTwoProgressesHaveTheSameCounts_TheyAreEqual()
    {
        // Arrange — a value object compares by value.
        var progress = BriefingProgress.FromCounts(4, 2);

        // Act
        var other = BriefingProgress.FromCounts(4, 2);

        // Assert
        other.Should().Be(progress);
    }
}
