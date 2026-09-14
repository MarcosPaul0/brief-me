using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Pins the two queries that feed the deadline monitor (RF028, BRF-043). They are what makes the sweep
/// idempotent — a demand already marked never comes back — and what decides which statuses are still the
/// design sector's to answer for.
/// </summary>
public class OverdueCandidateQueriesTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static Briefing NewBriefing(BriefingStatus status, DateTime? overdueSince = null)
    {
        var briefing = Briefing.Create("Campanha", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());
        briefing.CreatedAt = NowUtc.AddDays(-10);

        Force(briefing, nameof(Briefing.Status), status);

        if (overdueSince is not null)
            Force(briefing, nameof(Briefing.OverdueSince), overdueSince);

        return briefing;
    }

    private static BriefingTask NewTask(BriefingTaskStatus status, DateTime? overdueSince = null)
    {
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium);
        task.CreatedAt = NowUtc.AddDays(-10);

        Force(task, nameof(BriefingTask.Status), status);

        if (overdueSince is not null)
            Force(task, nameof(BriefingTask.OverdueSince), overdueSince);

        return task;
    }

    private static void Force(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.SetValue(entity, value);

    // --- Tasks -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public async Task GetOverdueCandidatesAsync_WhenTaskIsStillOpen_ReturnsIt(BriefingTaskStatus status)
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.Add(NewTask(status));
        await context.SaveChangesAsync();
        var repository = new BriefingTaskRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(1, 50);

        // Assert
        candidates.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOverdueCandidatesAsync_WhenTaskIsApproved_ExcludesIt()
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.Add(NewTask(BriefingTaskStatus.Approved));
        await context.SaveChangesAsync();
        var repository = new BriefingTaskRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(1, 50);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverdueCandidatesAsync_WhenTaskIsAlreadyMarked_ExcludesIt()
    {
        // Arrange
        // This exclusion is what makes "published exactly once" a fact of the query.
        await using var context = CreateContext();
        context.Tasks.Add(NewTask(BriefingTaskStatus.InProgress, overdueSince: NowUtc.AddDays(-1)));
        await context.SaveChangesAsync();
        var repository = new BriefingTaskRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(1, 50);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverdueCandidatesAsync_WhenAskedForTheSecondPage_ReturnsTheRemainingTasks()
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.AddRange(
            NewTask(BriefingTaskStatus.NotStarted),
            NewTask(BriefingTaskStatus.NotStarted),
            NewTask(BriefingTaskStatus.NotStarted));
        await context.SaveChangesAsync();
        var repository = new BriefingTaskRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(2, 2);

        // Assert
        candidates.Should().HaveCount(1);
    }

    // --- Briefings ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.InProgress)]
    public async Task GetOverdueCandidatesAsync_WhenBriefingIsStillOpen_ReturnsIt(BriefingStatus status)
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.Add(NewBriefing(status));
        await context.SaveChangesAsync();
        var repository = new BriefingRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(1, 50);

        // Assert
        candidates.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    [InlineData(BriefingStatus.Completed)]
    public async Task GetOverdueCandidatesAsync_WhenBriefingIsNotTheSectorsToAnswerFor_ExcludesIt(
        BriefingStatus status)
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.Add(NewBriefing(status));
        await context.SaveChangesAsync();
        var repository = new BriefingRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(1, 50);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverdueCandidatesAsync_WhenBriefingIsAlreadyMarked_ExcludesIt()
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.Add(NewBriefing(BriefingStatus.InProgress, overdueSince: NowUtc.AddDays(-1)));
        await context.SaveChangesAsync();
        var repository = new BriefingRepository(context);

        // Act
        var candidates = await repository.GetOverdueCandidatesAsync(1, 50);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenRulesAreConfigured_ReturnsTheWholePolicy()
    {
        // Arrange
        await using var context = CreateContext();
        context.SlaRules.AddRange(
            SlaRule.Create(null, null, 240),
            SlaRule.Create(Guid.NewGuid(), Priority.Urgent, 24));
        await context.SaveChangesAsync();
        var repository = new SlaRuleRepository(context);

        // Act
        var rules = await repository.GetAllAsync();

        // Assert
        rules.Should().HaveCount(2);
    }
}
