using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Pins the scope lookup of BRF-042: a scope is an exact key, and a null side means "any" literally — never
/// a wildcard that would let one scope answer for another.
/// </summary>
public class SlaRuleRepositoryTests
{
    private static readonly Guid VideoCategoryId = Guid.NewGuid();
    private static readonly Guid BannerCategoryId = Guid.NewGuid();

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>A context holding one rule of each of the four scopes, with a distinct deadline apiece.</summary>
    private static async Task<AppDbContext> CreateContextWithEveryScopeAsync()
    {
        var context = CreateContext();

        context.SlaRules.AddRange(
            SlaRule.Create(null, null, 240),
            SlaRule.Create(null, Priority.Urgent, 48),
            SlaRule.Create(VideoCategoryId, null, 120),
            SlaRule.Create(VideoCategoryId, Priority.Urgent, 24));
        await context.SaveChangesAsync();

        return context;
    }

    [Fact]
    public async Task GetByScopeAsync_WhenScopeIsGlobal_ReturnsTheGlobalRule()
    {
        // Arrange
        await using var context = await CreateContextWithEveryScopeAsync();
        var repository = new SlaRuleRepository(context);

        // Act
        var rule = await repository.GetByScopeAsync(null, null);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.Global);
        rule.Deadline.Hours.Should().Be(240);
    }

    [Fact]
    public async Task GetByScopeAsync_WhenScopeIsPriorityOnly_ReturnsThePriorityRule()
    {
        // Arrange
        await using var context = await CreateContextWithEveryScopeAsync();
        var repository = new SlaRuleRepository(context);

        // Act
        var rule = await repository.GetByScopeAsync(null, Priority.Urgent);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.Priority);
        rule.Deadline.Hours.Should().Be(48);
    }

    [Fact]
    public async Task GetByScopeAsync_WhenScopeIsCategoryOnly_ReturnsTheCategoryRule()
    {
        // Arrange
        await using var context = await CreateContextWithEveryScopeAsync();
        var repository = new SlaRuleRepository(context);

        // Act
        var rule = await repository.GetByScopeAsync(VideoCategoryId, null);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.Category);
        rule.Deadline.Hours.Should().Be(120);
    }

    [Fact]
    public async Task GetByScopeAsync_WhenScopeIsCategoryAndPriority_ReturnsTheNarrowestRule()
    {
        // Arrange
        await using var context = await CreateContextWithEveryScopeAsync();
        var repository = new SlaRuleRepository(context);

        // Act
        var rule = await repository.GetByScopeAsync(VideoCategoryId, Priority.Urgent);

        // Assert
        rule!.Scope.Should().Be(SlaRuleScope.CategoryAndPriority);
        rule.Deadline.Hours.Should().Be(24);
    }

    [Fact]
    public async Task GetByScopeAsync_WhenScopeHasNoRule_ReturnsNull()
    {
        // Arrange
        await using var context = await CreateContextWithEveryScopeAsync();
        var repository = new SlaRuleRepository(context);

        // Act
        var rule = await repository.GetByScopeAsync(BannerCategoryId, Priority.Low);

        // Assert
        rule.Should().BeNull();
    }

    [Fact]
    public async Task GetByScopeAsync_WhenOnlyTheGlobalRuleExists_DoesNotAnswerForAScopedLookup()
    {
        // Arrange
        await using var context = CreateContext();
        context.SlaRules.Add(SlaRule.Create(null, null, 240));
        await context.SaveChangesAsync();

        var repository = new SlaRuleRepository(context);

        // Act
        var rule = await repository.GetByScopeAsync(VideoCategoryId, Priority.Urgent);

        // Assert
        rule.Should().BeNull();
    }
}
