using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Pins the pair of queries behind the overdue task list (RF029, BRF-044). The mirror image of
/// <see cref="OverdueCandidateQueriesTests"/>: those read what the monitor still has to judge, these read
/// what it already marked.
/// </summary>
public class OverdueTaskQueriesTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
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

    /// <summary>
    /// The marking is the monitor's act and refuses some statuses; these tests need arbitrary
    /// status/mark combinations, so the state is written straight in — same approach as
    /// <see cref="OverdueCandidateQueriesTests"/>.
    /// </summary>
    private static void Force(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.SetValue(entity, value);

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public async Task GetOverdueAsync_WhenMarkedTaskIsStillOpen_ReturnsIt(BriefingTaskStatus status)
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.Add(NewTask(status, NowUtc.AddDays(-1)));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetOverdueAsync_WhenTaskIsNotMarked_ExcludesIt()
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.Add(NewTask(BriefingTaskStatus.InProgress));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverdueAsync_WhenMarkedTaskIsApproved_ExcludesItWithoutClearingTheMark()
    {
        // Arrange — the mark survives approval as the record of a late delivery (BRF-043), but approved work
        // needs no action, so it is out of this list.
        await using var context = CreateContext();
        var approved = NewTask(BriefingTaskStatus.Approved, NowUtc.AddDays(-1));
        context.Tasks.Add(approved);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Should().BeEmpty();
        (await repository.GetByIdAsync(approved.Id))!.OverdueSince.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOverdueAsync_WhenTasksWereMarkedAtDifferentTimes_ReturnsOldestMarkFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var newest = NewTask(BriefingTaskStatus.InProgress, NowUtc.AddHours(-1));
        var oldest = NewTask(BriefingTaskStatus.Blocked, NowUtc.AddDays(-5));
        context.Tasks.AddRange(newest, oldest);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Select(task => task.Id).Should().ContainInOrder(oldest.Id, newest.Id);
    }

    [Fact]
    public async Task GetOverdueAsync_WhenTasksShareTheSameMark_PagesWithoutRepeatingOrSkipping()
    {
        // Arrange — one sweep marks everything it finds with a single instant, so Id is what keeps the
        // pages apart.
        await using var context = CreateContext();
        var markedAt = NowUtc.AddDays(-2);
        context.Tasks.AddRange(
            NewTask(BriefingTaskStatus.InProgress, markedAt),
            NewTask(BriefingTaskStatus.InProgress, markedAt),
            NewTask(BriefingTaskStatus.InProgress, markedAt));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var firstPage = await repository.GetOverdueAsync(1, 2);
        var secondPage = await repository.GetOverdueAsync(2, 2);

        // Assert
        firstPage.Should().HaveCount(2);
        secondPage.Should().ContainSingle();
        firstPage.Select(task => task.Id).Should().NotIntersectWith(secondPage.Select(task => task.Id));
    }

    [Fact]
    public async Task CountOverdueAsync_WhenSomeTasksAreMarkedAndOpen_CountsOnlyThose()
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.AddRange(
            NewTask(BriefingTaskStatus.InProgress, NowUtc.AddDays(-1)),
            NewTask(BriefingTaskStatus.Blocked, NowUtc.AddDays(-3)),
            NewTask(BriefingTaskStatus.Approved, NowUtc.AddDays(-4)),
            NewTask(BriefingTaskStatus.InProgress));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var count = await repository.CountOverdueAsync();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountOverdueAsync_WhenNothingIsMarked_ReturnsZero()
    {
        // Arrange
        await using var context = CreateContext();
        context.Tasks.Add(NewTask(BriefingTaskStatus.InProgress));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var count = await repository.CountOverdueAsync();

        // Assert
        count.Should().Be(0);
    }
}
