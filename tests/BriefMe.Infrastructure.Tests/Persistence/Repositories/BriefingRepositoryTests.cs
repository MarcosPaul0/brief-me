using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class BriefingRepositoryTests
{
    private static AppDbContext CreateContext() => CreateContext(Guid.NewGuid().ToString());

    /// <summary>A context over a named store, so a test can save in one context and read in a fresh one.</summary>
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
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
            NewBriefing(requesterId, baseDate.AddDays(1), BriefingStatus.Submitted),
            NewBriefing(requesterId, baseDate.AddDays(2), BriefingStatus.Submitted));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByRequesterAsync(
            requesterId,
            new BriefingFilter(Status: BriefingStatus.Submitted),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(b => b.Status == BriefingStatus.Submitted);
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
            NewBriefing(requesterId, baseDate.AddDays(1), BriefingStatus.Submitted),
            NewBriefing(requesterId, baseDate.AddDays(2), BriefingStatus.Submitted));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var totalCount = await repository.CountByRequesterAsync(
            requesterId,
            new BriefingFilter(Status: BriefingStatus.Submitted));

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

    private static void SetDesigner(Briefing briefing, Guid designerId) =>
        typeof(Briefing).GetProperty(nameof(Briefing.DesignerId))!.SetValue(briefing, designerId);

    private static Briefing Claimed(Guid designerId, BriefingStatus status, DateOnly? desiredDate, DateTime createdAt)
    {
        var briefing = Briefing.Create("Briefing", Priority.Medium, Guid.NewGuid(), Guid.NewGuid(),
            desiredDeliveryDate: desiredDate);
        briefing.CreatedAt = createdAt;
        SetStatus(briefing, status);
        SetDesigner(briefing, designerId);
        return briefing;
    }

    [Fact]
    public async Task GetQueueAsync_WhenBriefingsHaveMixedStates_ReturnsOnlyUnclaimedSubmittedOldestFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var older = NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), BriefingStatus.Submitted);
        var newer = NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), BriefingStatus.Submitted);
        var draft = NewBriefing(Guid.NewGuid(), new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        var claimed = Claimed(Guid.NewGuid(), BriefingStatus.InProgress, null, new DateTime(2025, 12, 2, 0, 0, 0, DateTimeKind.Utc));
        await context.Briefings.AddRangeAsync(newer, draft, older, claimed);
        await context.SaveChangesAsync();
        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetQueueAsync(1, 10);

        // Assert
        result.Select(b => b.Id).Should().Equal(older.Id, newer.Id);
    }

    [Fact]
    public async Task CountQueueAsync_WhenPaged_CountsEveryQueuedBriefing()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Briefings.AddRangeAsync(
            NewBriefing(Guid.NewGuid(), DateTime.UtcNow, BriefingStatus.Submitted),
            NewBriefing(Guid.NewGuid(), DateTime.UtcNow, BriefingStatus.Submitted),
            NewBriefing(Guid.NewGuid(), DateTime.UtcNow, BriefingStatus.Cancelled));
        await context.SaveChangesAsync();

        // Act
        var result = await new BriefingRepository(context).CountQueueAsync();

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenDesignerHasWork_ReturnsOnlyTheirsByDesiredDateWithUndatedLast()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var undated = Claimed(designerId, BriefingStatus.InProgress, null, created);
        var later = Claimed(designerId, BriefingStatus.InProgress, new DateOnly(2026, 3, 1), created);
        var sooner = Claimed(designerId, BriefingStatus.Delivered, new DateOnly(2026, 2, 1), created);
        var someoneElses = Claimed(Guid.NewGuid(), BriefingStatus.InProgress, new DateOnly(2026, 1, 15), created);
        await context.Briefings.AddRangeAsync(undated, later, sooner, someoneElses);
        await context.SaveChangesAsync();

        // Act
        var result = await new BriefingRepository(context).GetByDesignerAsync(designerId, null, 1, 10);

        // Assert
        result.Select(b => b.Id).Should().Equal(sooner.Id, later.Id, undated.Id);
    }

    [Fact]
    public async Task CountByDesignerAsync_WhenFilteredByStatus_CountsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        await context.Briefings.AddRangeAsync(
            Claimed(designerId, BriefingStatus.InProgress, null, DateTime.UtcNow),
            Claimed(designerId, BriefingStatus.Delivered, null, DateTime.UtcNow));
        await context.SaveChangesAsync();

        // Act
        var result = await new BriefingRepository(context).CountByDesignerAsync(designerId, BriefingStatus.Delivered);

        // Assert
        result.Should().Be(1);
    }

    private static async Task<Briefing> SeedDraftWithAttachmentsAsync(string databaseName, params string[] fileNames)
    {
        await using var context = CreateContext(databaseName);
        var briefing = Briefing.Create("Briefing", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());
        foreach (var fileName in fileNames)
            briefing.AddAttachment(fileName, $"briefings/{Guid.NewGuid():N}.pdf", AttachmentFormat.Pdf, 100);

        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();
        return briefing;
    }

    [Fact]
    public async Task GetByIdWithAttachmentsAsync_WhenBriefingHasAttachments_LoadsThem()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var seeded = await SeedDraftWithAttachmentsAsync(databaseName, "a.pdf", "b.pdf");
        await using var context = CreateContext(databaseName);
        var repository = new BriefingRepository(context);

        // Act
        var briefing = await repository.GetByIdWithAttachmentsAsync(seeded.Id);

        // Assert
        briefing!.Attachments.Select(a => a.FileName).Should().BeEquivalentTo(["a.pdf", "b.pdf"]);
    }

    [Fact]
    public async Task GetByIdWithAttachmentsAsync_WhenBriefingDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);

        // Act
        var briefing = await repository.GetByIdWithAttachmentsAsync(Guid.NewGuid());

        // Assert
        briefing.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenBriefingHasAttachments_DoesNotLoadThem()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var seeded = await SeedDraftWithAttachmentsAsync(databaseName, "a.pdf");
        await using var context = CreateContext(databaseName);
        var repository = new BriefingRepository(context);

        // Act
        var briefing = await repository.GetByIdAsync(seeded.Id);

        // Assert
        briefing!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChanges_WhenAttachmentAddedToUpdatedTrackedBriefing_InsertsIt()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var seeded = await SeedDraftWithAttachmentsAsync(databaseName, "a.pdf");
        await using (var context = CreateContext(databaseName))
        {
            var repository = new BriefingRepository(context);
            var briefing = await repository.GetByIdWithAttachmentsAsync(seeded.Id);
            repository.Update(briefing!);
            briefing!.AddAttachment("b.png", "briefings/b.png", AttachmentFormat.Png, 50);

            // Act
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verify = CreateContext(databaseName);
        var reloaded = await new BriefingRepository(verify).GetByIdWithAttachmentsAsync(seeded.Id);
        reloaded!.Attachments.Select(a => a.FileName).Should().BeEquivalentTo(["a.pdf", "b.png"]);
    }

    [Fact]
    public async Task SaveChanges_WhenAttachmentRemovedFromTrackedBriefing_DeletesIt()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var seeded = await SeedDraftWithAttachmentsAsync(databaseName, "a.pdf", "b.pdf");
        var droppedId = seeded.Attachments.Single(a => a.FileName == "a.pdf").Id;
        await using (var context = CreateContext(databaseName))
        {
            var repository = new BriefingRepository(context);
            var briefing = await repository.GetByIdWithAttachmentsAsync(seeded.Id);
            briefing!.RemoveAttachment(droppedId);

            // Act
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verify = CreateContext(databaseName);
        verify.Set<BriefingAttachment>().Select(a => a.FileName).Should().BeEquivalentTo(["b.pdf"]);
    }
}
