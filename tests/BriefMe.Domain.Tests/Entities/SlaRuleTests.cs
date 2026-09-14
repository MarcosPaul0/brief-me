using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class SlaRuleTests
{
    private static readonly Guid VideoCategoryId = Guid.NewGuid();
    private static readonly Guid BannerCategoryId = Guid.NewGuid();

    [Fact]
    public void Create_WhenScopeAndDeadlineAreValid_ReturnsRuleWithTheConfiguredDeadline()
    {
        // Arrange
        const int deadlineHours = 24;

        // Act
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, deadlineHours);

        // Assert
        rule.CategoryId.Should().Be(VideoCategoryId);
        rule.Priority.Should().Be(Priority.Urgent);
        rule.Deadline.Should().Be(SlaDeadline.FromPersistedValue(deadlineHours));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenDeadlineHoursIsNotPositive_ThrowsDomainException(int deadlineHours)
    {
        // Arrange
        // (none)

        // Act
        var act = () => SlaRule.Create(VideoCategoryId, Priority.High, deadlineHours);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenDeadlineHoursExceedsMax_ThrowsDomainException()
    {
        // Arrange
        var deadlineHours = SlaDeadline.MaxHours + 1;

        // Act
        var act = () => SlaRule.Create(null, null, deadlineHours);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenPriorityIsOutsideTheEnum_ThrowsDomainException()
    {
        // Arrange
        const Priority unknownPriority = (Priority)99;

        // Act
        var act = () => SlaRule.Create(null, unknownPriority, 48);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenCategoryIdIsEmpty_TreatsTheRuleAsUnscopedByCategory()
    {
        // Arrange
        // (none)

        // Act
        var rule = SlaRule.Create(Guid.Empty, Priority.Low, 240);

        // Assert
        rule.CategoryId.Should().BeNull();
    }

    [Fact]
    public void Scope_WhenNeitherCategoryNorPriorityIsSet_IsGlobal()
    {
        // Arrange
        var rule = SlaRule.Create(null, null, 240);

        // Act
        var scope = rule.Scope;

        // Assert
        scope.Should().Be(SlaRuleScope.Global);
    }

    [Fact]
    public void Scope_WhenOnlyCategoryIsSet_IsCategory()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, null, 120);

        // Act
        var scope = rule.Scope;

        // Assert
        scope.Should().Be(SlaRuleScope.Category);
    }

    [Fact]
    public void Scope_WhenOnlyPriorityIsSet_IsPriority()
    {
        // Arrange
        var rule = SlaRule.Create(null, Priority.Urgent, 48);

        // Act
        var scope = rule.Scope;

        // Assert
        scope.Should().Be(SlaRuleScope.Priority);
    }

    [Fact]
    public void Scope_WhenCategoryAndPriorityAreSet_IsCategoryAndPriority()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var scope = rule.Scope;

        // Assert
        scope.Should().Be(SlaRuleScope.CategoryAndPriority);
    }

    [Fact]
    public void Scope_WhenScopesAreRanked_OrdersFromGlobalToCategoryAndPriority()
    {
        // Arrange — the ranking is what makes "the most specific matching rule" a single answer (BRF-043).
        var global = SlaRule.Create(null, null, 240);
        var byPriority = SlaRule.Create(null, Priority.Urgent, 48);
        var byCategory = SlaRule.Create(VideoCategoryId, null, 120);
        var byBoth = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var mostSpecific = new[] { global, byPriority, byCategory, byBoth }.MaxBy(rule => rule.Scope);

        // Assert
        ((int)global.Scope).Should().BeLessThan((int)byPriority.Scope);
        ((int)byPriority.Scope).Should().BeLessThan((int)byCategory.Scope);
        ((int)byCategory.Scope).Should().BeLessThan((int)byBoth.Scope);
        mostSpecific.Should().Be(byBoth);
    }

    [Theory]
    [InlineData(Priority.Low)]
    [InlineData(Priority.Medium)]
    [InlineData(Priority.High)]
    [InlineData(Priority.Urgent)]
    public void AppliesTo_WhenRuleIsGlobal_MatchesEveryDemand(Priority priority)
    {
        // Arrange
        var rule = SlaRule.Create(null, null, 240);

        // Act
        var applies = rule.AppliesTo(BannerCategoryId, priority);

        // Assert
        applies.Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_WhenRuleIsScopedByCategory_MatchesAnyPriorityOfThatCategory()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, null, 120);

        // Act
        var applies = rule.AppliesTo(VideoCategoryId, Priority.Low);

        // Assert
        applies.Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_WhenRuleIsScopedByCategory_DoesNotMatchAnotherCategory()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, null, 120);

        // Act
        var applies = rule.AppliesTo(BannerCategoryId, Priority.Low);

        // Assert
        applies.Should().BeFalse();
    }

    [Fact]
    public void AppliesTo_WhenRuleIsScopedByPriority_MatchesThatPriorityInAnyCategory()
    {
        // Arrange
        var rule = SlaRule.Create(null, Priority.Urgent, 48);

        // Act
        var applies = rule.AppliesTo(BannerCategoryId, Priority.Urgent);

        // Assert
        applies.Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_WhenRuleIsScopedByPriority_DoesNotMatchAnotherPriority()
    {
        // Arrange
        var rule = SlaRule.Create(null, Priority.Urgent, 48);

        // Act
        var applies = rule.AppliesTo(BannerCategoryId, Priority.Medium);

        // Assert
        applies.Should().BeFalse();
    }

    [Fact]
    public void AppliesTo_WhenRuleIsScopedByCategoryAndPriority_RequiresBothToMatch()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var applies = rule.AppliesTo(VideoCategoryId, Priority.Urgent);

        // Assert
        applies.Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_WhenRuleIsScopedByCategoryAndPriority_DoesNotMatchTheCategoryAlone()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var applies = rule.AppliesTo(VideoCategoryId, Priority.Low);

        // Assert
        applies.Should().BeFalse();
    }

    [Fact]
    public void ChangeDeadline_WhenHoursAreValid_ReplacesTheDeadline()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        rule.ChangeDeadline(12);

        // Assert
        rule.Deadline.Should().Be(SlaDeadline.FromPersistedValue(12));
    }

    [Fact]
    public void ChangeDeadline_WhenHoursAreValid_KeepsTheScopeUntouched()
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        rule.ChangeDeadline(12);

        // Assert
        rule.CategoryId.Should().Be(VideoCategoryId);
        rule.Priority.Should().Be(Priority.Urgent);
        rule.Scope.Should().Be(SlaRuleScope.CategoryAndPriority);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(SlaDeadline.MaxHours + 1)]
    public void ChangeDeadline_WhenHoursAreOutsideTheAcceptedRange_ThrowsDomainException(int deadlineHours)
    {
        // Arrange
        var rule = SlaRule.Create(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var act = () => rule.ChangeDeadline(deadlineHours);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangeDeadline_WhenHoursAreOutsideTheAcceptedRange_LeavesTheCurrentDeadlineIntact()
    {
        // Arrange
        var rule = SlaRule.Create(null, null, 240);

        // Act
        var act = () => rule.ChangeDeadline(0);

        // Assert
        act.Should().Throw<DomainException>();
        rule.Deadline.Should().Be(SlaDeadline.FromPersistedValue(240));
    }
}
