using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Pins the pair of queries behind the overdue briefing list (RF030, BRF-045). The mirror image of
/// <see cref="OverdueCandidateQueriesTests"/>: those read what the monitor still has to judge, these read
/// what it already marked.
/// </summary>
public class OverdueBriefingQueriesTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

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

    /// <summary>
    /// The marking is the monitor's act and refuses some statuses; these tests need arbitrary status/mark
    /// combinations, so the state is written straight in — same approach as
    /// <see cref="OverdueCandidateQueriesTests"/>.
    /// </summary>
    private static void Force(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.SetValue(entity, value);

    [Theory]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.InProgress)]
    public async Task GetOverdueAsync_WhenMarkedBriefingIsStillOpen_ReturnsIt(BriefingStatus status)
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.Add(NewBriefing(status, NowUtc.AddDays(-1)));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetOverdueAsync_WhenBriefingIsNotMarked_ExcludesIt()
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.Add(NewBriefing(BriefingStatus.InProgress));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    [InlineData(BriefingStatus.Completed)]
    public async Task GetOverdueAsync_WhenMarkedBriefingIsClosed_ExcludesItWithoutClearingTheMark(
        BriefingStatus status)
    {
        // Arrange — the mark survives the closing statuses as the record of a late demand (BRF-043), but a
        // closed briefing needs no action, so it is out of this list.
        await using var context = CreateContext();
        var closed = NewBriefing(status, NowUtc.AddDays(-1));
        context.Briefings.Add(closed);
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Should().BeEmpty();
        (await repository.GetByIdAsync(closed.Id))!.OverdueSince.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOverdueAsync_WhenBriefingsWereMarkedAtDifferentTimes_ReturnsOldestMarkFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var newest = NewBriefing(BriefingStatus.InProgress, NowUtc.AddHours(-1));
        var oldest = NewBriefing(BriefingStatus.AwaitingApproval, NowUtc.AddDays(-5));
        context.Briefings.AddRange(newest, oldest);
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetOverdueAsync(1, 20);

        // Assert
        result.Select(briefing => briefing.Id).Should().ContainInOrder(oldest.Id, newest.Id);
    }

    [Fact]
    public async Task GetOverdueAsync_WhenBriefingsShareTheSameMark_PagesWithoutRepeatingOrSkipping()
    {
        // Arrange — one sweep marks everything it finds with a single instant, so Id is what keeps the pages
        // apart.
        await using var context = CreateContext();
        var markedAt = NowUtc.AddDays(-2);
        context.Briefings.AddRange(
            NewBriefing(BriefingStatus.InProgress, markedAt),
            NewBriefing(BriefingStatus.InProgress, markedAt),
            NewBriefing(BriefingStatus.InProgress, markedAt));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var firstPage = await repository.GetOverdueAsync(1, 2);
        var secondPage = await repository.GetOverdueAsync(2, 2);

        // Assert
        firstPage.Should().HaveCount(2);
        secondPage.Should().ContainSingle();
        firstPage.Select(b => b.Id).Should().NotIntersectWith(secondPage.Select(b => b.Id));
    }

    [Fact]
    public async Task CountOverdueAsync_WhenSomeBriefingsAreMarkedAndOpen_CountsOnlyThose()
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.AddRange(
            NewBriefing(BriefingStatus.InProgress, NowUtc.AddDays(-1)),
            NewBriefing(BriefingStatus.AwaitingApproval, NowUtc.AddDays(-3)),
            NewBriefing(BriefingStatus.Completed, NowUtc.AddDays(-4)),
            NewBriefing(BriefingStatus.InProgress));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

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
        context.Briefings.Add(NewBriefing(BriefingStatus.InProgress));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var count = await repository.CountOverdueAsync();

        // Assert
        count.Should().Be(0);
    }
}
