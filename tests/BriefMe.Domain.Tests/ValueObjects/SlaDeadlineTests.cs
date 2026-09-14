using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class SlaDeadlineTests
{
    [Fact]
    public void Create_WhenHoursIsWithinRange_ReturnsSuccess()
    {
        // Arrange
        const int hours = 48;

        // Act
        var result = SlaDeadline.Create(hours);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Hours.Should().Be(hours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-240)]
    public void Create_WhenHoursIsNotPositive_ReturnsFailure(int hours)
    {
        // Arrange
        // (none)

        // Act
        var result = SlaDeadline.Create(hours);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenHoursExceedsMax_ReturnsFailure()
    {
        // Arrange
        var hours = SlaDeadline.MaxHours + 1;

        // Act
        var result = SlaDeadline.Create(hours);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(SlaDeadline.MinHours)]
    [InlineData(SlaDeadline.MaxHours)]
    public void Create_WhenHoursIsAtBoundary_ReturnsSuccess(int hours)
    {
        // Arrange
        // (none)

        // Act
        var result = SlaDeadline.Create(hours);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DueAtFrom_WhenGivenAStartInstant_AddsTheConfiguredHours()
    {
        // Arrange
        var deadline = SlaDeadline.Create(24).Value;
        var startedAt = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var dueAt = deadline.DueAtFrom(startedAt);

        // Assert
        dueAt.Should().Be(new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DueAtFrom_WhenDeadlineSpansSeveralDays_CountsRunningHours()
    {
        // Arrange — 120h from a Friday lands on the following Wednesday: no business calendar (RF027).
        var deadline = SlaDeadline.Create(120).Value;
        var startedAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var dueAt = deadline.DueAtFrom(startedAt);

        // Assert
        dueAt.Should().Be(new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Equals_WhenHoursMatch_ReturnsTrue()
    {
        // Arrange
        var first = SlaDeadline.Create(72).Value;
        var second = SlaDeadline.Create(72).Value;

        // Act
        var areEqual = first.Equals(second);

        // Assert
        areEqual.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenHoursDiffer_ReturnsFalse()
    {
        // Arrange
        var first = SlaDeadline.Create(72).Value;
        var second = SlaDeadline.Create(24).Value;

        // Act
        var areEqual = first.Equals(second);

        // Assert
        areEqual.Should().BeFalse();
    }

    [Fact]
    public void FromPersistedValue_WhenRehydratingFromTheDatabase_KeepsTheStoredHours()
    {
        // Arrange
        const int storedHours = 36;

        // Act
        var deadline = SlaDeadline.FromPersistedValue(storedHours);

        // Assert
        deadline.Hours.Should().Be(storedHours);
    }
}
