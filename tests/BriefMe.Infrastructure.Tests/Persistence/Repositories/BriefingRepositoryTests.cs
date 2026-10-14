using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class BriefingRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static Briefing NewBriefing(
        Guid requesterId,
        DateTime createdAt,
        BriefingStatus status = BriefingStatus.Draft,
        Guid? categoryId = null)
    {
        var briefing = Briefing.Create("Briefing", Priority.Medium, categoryId ?? Guid.NewGuid(), requesterId);
        briefing.CreatedAt = createdAt;

        if (status != BriefingStatus.Draft)
            SetStatus(briefing, status);

        return briefing;
    }

    /// <summary>
    /// Puts a briefing into a status its transition methods would only reach through the whole lifecycle.
    /// These tests are about the repository's queries, not about how a briefing gets there.
    /// </summary>
    private static void SetStatus(Briefing briefing, BriefingStatus status) =>
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, status);

    [Fact]
    public async Task GetByRequesterAsync_WhenUserHasBriefings_ReturnsOnlyOwnBriefings()
    {
        // Arrange
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(requesterId, baseDate),
            NewBriefing(requesterId, baseDate.AddDays(1)),
            NewBriefing(requesterId, baseDate.AddDays(2)),
            NewBriefing(otherId, baseDate.AddDays(3)));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByRequesterAsync(requesterId, new BriefingFilter(), page: 1, pageSize: 10);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(b => b.RequesterId == requesterId);
    }

    [Fact]
    public async Task GetByRequesterAsync_WhenFilteredByStatus_ReturnsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(requesterId, baseDate, BriefingStatus.Draft),
            NewBriefing(requesterId, baseDate.AddDays(1), BriefingStatus.AwaitingApproval),
            NewBriefing(requesterId, baseDate.AddDays(2), BriefingStatus.AwaitingApproval));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByRequesterAsync(
            requesterId,
            new BriefingFilter(Status: BriefingStatus.AwaitingApproval),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(b => b.Status == BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task GetByRequesterAsync_WhenFilteredByCategory_ReturnsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(requesterId, baseDate, categoryId: categoryId),
            NewBriefing(requesterId, baseDate.AddDays(1), categoryId: Guid.NewGuid()));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByRequesterAsync(
            requesterId,
            new BriefingFilter(CategoryId: categoryId),
            page: 1,
            pageSize: 10);

        // Assert
        result.Single().CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task GetByRequesterAsync_WhenPaged_ReturnsSliceOrderedByCreatedAtDescending()
    {
        // Arrange
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var oldest = NewBriefing(requesterId, baseDate);
        var middle = NewBriefing(requesterId, baseDate.AddDays(1));
        var newest = NewBriefing(requesterId, baseDate.AddDays(2));
        context.Briefings.AddRange(oldest, middle, newest);
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByRequesterAsync(requesterId, new BriefingFilter(), page: 1, pageSize: 2);

        // Assert
        result.Select(b => b.Id).Should().ContainInOrder(newest.Id, middle.Id);
    }

    [Fact]
    public async Task CountByRequesterAsync_WhenResultIsPaged_CountsEveryMatchNotJustThePage()
    {
        // Arrange
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(requesterId, baseDate),
            NewBriefing(requesterId, baseDate.AddDays(1)),
            NewBriefing(requesterId, baseDate.AddDays(2)),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(3)));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var totalCount = await repository.CountByRequesterAsync(requesterId, new BriefingFilter());

        // Assert
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task CountByRequesterAsync_WhenFilteredByStatus_CountsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(requesterId, baseDate, BriefingStatus.Draft),
            NewBriefing(requesterId, baseDate.AddDays(1), BriefingStatus.AwaitingApproval),
            NewBriefing(requesterId, baseDate.AddDays(2), BriefingStatus.AwaitingApproval));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var totalCount = await repository.CountByRequesterAsync(
            requesterId,
            new BriefingFilter(Status: BriefingStatus.AwaitingApproval));

        // Assert
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByRequesterAsync_WhenUserHasNoBriefings_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByRequesterAsync(Guid.NewGuid(), new BriefingFilter(), page: 1, pageSize: 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsBriefing()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);
        var briefing = NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act
        await repository.AddAsync(briefing);
        await context.SaveChangesAsync();
        var fetched = await repository.GetByIdAsync(briefing.Id);

        // Assert
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(briefing.Id);
    }
}
