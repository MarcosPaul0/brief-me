using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Services;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Services;

/// <summary>
/// Pins the one definition of "when is this demand due?" (RF028, BRF-043): which SLA rule wins among the
/// ones that match, and how that rule's deadline combines with the date typed onto the demand.
/// </summary>
public class SlaDeadlineResolverTests
{
    private static readonly Guid VideoCategoryId = Guid.NewGuid();
    private static readonly Guid BannerCategoryId = Guid.NewGuid();
    private static readonly DateTime StartedAtUtc = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly SlaDeadlineResolver _resolver = new();

    [Fact]
    public void ResolveRule_WhenSeveralRulesMatch_ReturnsTheMostSpecificOne()
    {
        // Arrange
        SlaRule[] rules =
        [
            SlaRule.Create(null, null, 240),
            SlaRule.Create(null, Priority.Urgent, 48),
            SlaRule.Create(VideoCategoryId, null, 120),
            SlaRule.Create(VideoCategoryId, Priority.Urgent, 24)
        ];

        // Act
        var rule = _resolver.ResolveRule(rules, VideoCategoryId, Priority.Urgent);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.CategoryAndPriority);
    }

    [Fact]
    public void ResolveRule_WhenCategoryAndPriorityRulesCompete_CategoryWins()
    {
        // Arrange
        SlaRule[] rules =
        [
            SlaRule.Create(null, Priority.Urgent, 48),
            SlaRule.Create(VideoCategoryId, null, 120)
        ];

        // Act
        var rule = _resolver.ResolveRule(rules, VideoCategoryId, Priority.Urgent);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.Category);
    }

    [Fact]
    public void ResolveRule_WhenOnlyTheGlobalRuleMatches_ReturnsTheGlobalRule()
    {
        // Arrange
        SlaRule[] rules =
        [
            SlaRule.Create(null, null, 240),
            SlaRule.Create(BannerCategoryId, Priority.Urgent, 24)
        ];

        // Act
        var rule = _resolver.ResolveRule(rules, VideoCategoryId, Priority.Low);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.Global);
    }

    [Fact]
    public void ResolveRule_WhenNoRuleMatches_ReturnsNull()
    {
        // Arrange
        SlaRule[] rules = [SlaRule.Create(BannerCategoryId, null, 24)];

        // Act
        var rule = _resolver.ResolveRule(rules, VideoCategoryId, Priority.Low);

        // Assert
        rule.Should().BeNull();
    }

    [Fact]
    public void ResolveRule_WhenThePolicyTableIsEmpty_ReturnsNull()
    {
        // Arrange
        // (an empty policy is a freshly installed system, not an error)

        // Act
        var rule = _resolver.ResolveRule([], VideoCategoryId, Priority.High);

        // Assert
        rule.Should().BeNull();
    }

    [Fact]
    public void ResolveDueAt_WhenTheSlaComesFirst_ReturnsTheSlaInstant()
    {
        // Arrange
        SlaRule[] rules = [SlaRule.Create(VideoCategoryId, Priority.Urgent, 24)];
        var typedDeadline = new DateOnly(2026, 8, 10);

        // Act
        var dueAt = _resolver.ResolveDueAt(rules, VideoCategoryId, Priority.Urgent, StartedAtUtc, typedDeadline);

        // Assert
        dueAt.Should().Be(StartedAtUtc.AddHours(24));
    }

    [Fact]
    public void ResolveDueAt_WhenTheTypedDeadlineComesFirst_ReturnsTheEndOfThatDay()
    {
        // Arrange
        SlaRule[] rules = [SlaRule.Create(VideoCategoryId, Priority.Urgent, 240)];
        var typedDeadline = new DateOnly(2026, 8, 3);

        // Act
        var dueAt = _resolver.ResolveDueAt(rules, VideoCategoryId, Priority.Urgent, StartedAtUtc, typedDeadline);

        // Assert
        dueAt.Should().Be(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolveDueAt_WhenThereIsNoTypedDeadline_ReturnsTheSlaInstant()
    {
        // Arrange
        SlaRule[] rules = [SlaRule.Create(null, null, 72)];

        // Act
        var dueAt = _resolver.ResolveDueAt(rules, VideoCategoryId, Priority.Medium, StartedAtUtc, null);

        // Assert
        dueAt.Should().Be(StartedAtUtc.AddHours(72));
    }

    [Fact]
    public void ResolveDueAt_WhenNoRuleApplies_ReturnsTheTypedDeadlineAlone()
    {
        // Arrange
        SlaRule[] rules = [SlaRule.Create(BannerCategoryId, null, 24)];

        // Act
        var dueAt = _resolver.ResolveDueAt(
            rules,
            VideoCategoryId,
            Priority.Medium,
            StartedAtUtc,
            new DateOnly(2026, 8, 5));

        // Assert
        dueAt.Should().Be(new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolveDueAt_WhenTheSlaClockHasNotStarted_ReturnsTheTypedDeadlineAlone()
    {
        // Arrange
        // A briefing still with the requester has no SLA start, but keeps its requested date.
        SlaRule[] rules = [SlaRule.Create(null, null, 1)];

        // Act
        var dueAt = _resolver.ResolveDueAt(
            rules,
            VideoCategoryId,
            Priority.Medium,
            slaStartedAtUtc: null,
            explicitDeadline: new DateOnly(2026, 8, 5));

        // Assert
        dueAt.Should().Be(new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolveDueAt_WhenTheDemandCarriesNoDeadlineAtAll_ReturnsNull()
    {
        // Arrange
        // (no rule matches, no date typed — nothing to breach)

        // Act
        var dueAt = _resolver.ResolveDueAt([], VideoCategoryId, Priority.Medium, StartedAtUtc, null);

        // Assert
        dueAt.Should().BeNull();
    }

    [Fact]
    public void EndOfDayUtc_WhenGivenADate_ReturnsMidnightOfTheFollowingDay()
    {
        // Arrange
        var date = new DateOnly(2026, 8, 2);

        // Act
        var instant = SlaDeadlineResolver.EndOfDayUtc(date);

        // Assert
        instant.Should().Be(new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void EndOfDayUtc_WhenGivenADate_ReturnsAnInstantMarkedAsUtc()
    {
        // Arrange
        var date = new DateOnly(2026, 8, 2);

        // Act
        var instant = SlaDeadlineResolver.EndOfDayUtc(date);

        // Assert
        instant.Kind.Should().Be(DateTimeKind.Utc);
    }
}
