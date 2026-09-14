using BriefMe.Web.Components.Sla;
using FluentAssertions;

namespace BriefMe.Web.Tests.Sla;

public class OverdueLabelsTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "há menos de 1 dia")]
    [InlineData(23, "há menos de 1 dia")]
    [InlineData(24, "há 1 dia")]
    [InlineData(47, "há 1 dia")]
    [InlineData(48, "há 2 dias")]
    [InlineData(240, "há 10 dias")]
    public void Elapsed_WhenMarkedInThePast_ReadsInWholeDays(int hoursAgo, string expected)
    {
        // Arrange
        var overdueSince = Now.AddHours(-hoursAgo);

        // Act
        var label = OverdueLabels.Elapsed(overdueSince, Now);

        // Assert
        label.Should().Be(expected);
    }

    [Fact]
    public void Elapsed_WhenMarkIsAheadOfTheClock_NeverReadsNegative()
    {
        // Arrange — clock skew between the browser and the server that wrote the mark.
        var overdueSince = Now.AddHours(2);

        // Act
        var label = OverdueLabels.Elapsed(overdueSince, Now);

        // Assert
        label.Should().Be("há menos de 1 dia");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(71)]
    public void SeverityClass_WhenDelayIsUnderThreeDays_UsesTheWarningTint(int hoursAgo)
    {
        // Arrange
        var overdueSince = Now.AddHours(-hoursAgo);

        // Act
        var cssClass = OverdueLabels.SeverityClass(overdueSince, Now);

        // Assert
        cssClass.Should().Be("text-[#ff7a3d]");
    }

    [Theory]
    [InlineData(72)]
    [InlineData(240)]
    public void SeverityClass_WhenDelayReachesThreeDays_UsesTheBreachTint(int hoursAgo)
    {
        // Arrange
        var overdueSince = Now.AddHours(-hoursAgo);

        // Act
        var cssClass = OverdueLabels.SeverityClass(overdueSince, Now);

        // Assert
        cssClass.Should().Be("text-[#ef4444]");
    }
}
