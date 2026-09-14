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
    public async Task GetPendingApprovalAsync_WhenBriefingsHaveMixedStatuses_ReturnsOnlyAwaitingApproval()
    {
        // Arrange
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(Guid.NewGuid(), baseDate, BriefingStatus.AwaitingApproval),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(1), BriefingStatus.AwaitingApproval),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(2)),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(3), BriefingStatus.Approved));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetPendingApprovalAsync(page: 1, pageSize: 10);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(b => b.Status == BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task GetPendingApprovalAsync_WhenQueueHasSeveralBriefings_ReturnsOldestFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var oldest = NewBriefing(Guid.NewGuid(), baseDate, BriefingStatus.AwaitingApproval);
        var middle = NewBriefing(Guid.NewGuid(), baseDate.AddDays(1), BriefingStatus.AwaitingApproval);
        var newest = NewBriefing(Guid.NewGuid(), baseDate.AddDays(2), BriefingStatus.AwaitingApproval);

        context.Briefings.AddRange(newest, oldest, middle);
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetPendingApprovalAsync(page: 1, pageSize: 10);

        // Assert
        result.Select(b => b.Id).Should().ContainInOrder(oldest.Id, middle.Id, newest.Id);
    }

    [Fact]
    public async Task GetPendingApprovalAsync_WhenPaginated_ReturnsRequestedPageWithTotalCount()
    {
        // Arrange
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var oldest = NewBriefing(Guid.NewGuid(), baseDate, BriefingStatus.AwaitingApproval);
        var middle = NewBriefing(Guid.NewGuid(), baseDate.AddDays(1), BriefingStatus.AwaitingApproval);
        var newest = NewBriefing(Guid.NewGuid(), baseDate.AddDays(2), BriefingStatus.AwaitingApproval);

        context.Briefings.AddRange(oldest, middle, newest);
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetPendingApprovalAsync(page: 2, pageSize: 2);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(newest.Id);
    }

    [Fact]
    public async Task CountPendingApprovalAsync_WhenBriefingsHaveMixedStatuses_CountsAwaitingApprovalOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Briefings.AddRange(
            NewBriefing(Guid.NewGuid(), baseDate, BriefingStatus.AwaitingApproval),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(1), BriefingStatus.AwaitingApproval),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(2)),
            NewBriefing(Guid.NewGuid(), baseDate.AddDays(3), BriefingStatus.Approved));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var totalCount = await repository.CountPendingApprovalAsync();

        // Assert
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingApprovalAsync_WhenNothingIsPending_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        context.Briefings.Add(NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetPendingApprovalAsync(page: 1, pageSize: 10);

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

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsRejectionDecision()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);
        var managerId = Guid.NewGuid();
        var briefing = NewBriefing(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BriefingStatus.AwaitingApproval);
        briefing.Reject("Budget is not approved for this quarter.", managerId);

        // Act
        await repository.AddAsync(briefing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var fetched = await repository.GetByIdAsync(briefing.Id);

        // Assert
        fetched!.DecisionJustification!.Value.Should().Be("Budget is not approved for this quarter.");
        fetched.ReviewedById.Should().Be(managerId);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenSomeIdsExist_ReturnsOnlyTheMatchingBriefings()
    {
        // Arrange
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var wanted = NewBriefing(Guid.NewGuid(), baseDate);
        var other = NewBriefing(Guid.NewGuid(), baseDate.AddDays(1));

        context.Briefings.AddRange(wanted, other);
        await context.SaveChangesAsync();

        var repository = new BriefingRepository(context);

        // Act — one id exists, one does not (a task whose briefing is gone must not fail the lookup).
        var result = await repository.GetByIdsAsync([wanted.Id, Guid.NewGuid()]);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(wanted.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenIdsAreEmpty_ReturnsEmptyWithoutQuerying()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeliveriesByTaskAsync_WhenTaskHasDeliveries_ReturnsThemNewestFirst()
    {
        // Arrange
        await using var context = CreateContext();
        // Deliveries are only accepted while the briefing is in execution (RF022).
        var briefing = NewBriefing(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BriefingStatus.InProgress);
        var taskId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = StampDelivery(briefing.AddDelivery(taskId, "v1.png", "key-1", 10, uploaderId), baseDate);
        var second = StampDelivery(briefing.AddDelivery(taskId, "v2.png", "key-2", 20, uploaderId), baseDate.AddHours(1));

        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetDeliveriesByTaskAsync(taskId);

        // Assert
        result.Select(a => a.Id).Should().ContainInOrder(second.Id, first.Id);
    }

    [Fact]
    public async Task GetDeliveriesByTaskAsync_WhenBriefingAlsoHasOtherAttachments_ReturnsOnlyThatTasksDeliveries()
    {
        // Arrange
        await using var context = CreateContext();
        var briefing = NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var taskId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();

        // A reference file attached to the briefing itself (RF005) — only accepted before execution starts.
        briefing.AddAttachment("referencia.pdf", "key-3", 10, uploaderId);
        SetStatus(briefing, BriefingStatus.InProgress);

        var wanted = briefing.AddDelivery(taskId, "v1.png", "key-1", 10, uploaderId);
        // Another task's delivery on the same briefing.
        briefing.AddDelivery(Guid.NewGuid(), "outra.png", "key-2", 10, uploaderId);

        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetDeliveriesByTaskAsync(taskId);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(wanted.Id);
    }

    [Fact]
    public async Task GetByIdWithAttachmentsAsync_WhenBriefingAlsoHasDeliveries_ReturnsOnlyItsOwnAttachments()
    {
        // Arrange
        await using var context = CreateContext();
        var briefing = NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var uploaderId = Guid.NewGuid();

        var reference = briefing.AddAttachment("referencia.pdf", "key-1", 10, uploaderId);
        SetStatus(briefing, BriefingStatus.InProgress);
        briefing.AddDelivery(Guid.NewGuid(), "entrega.png", "key-2", 10, uploaderId);

        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByIdWithAttachmentsAsync(briefing.Id);

        // Assert
        result!.Attachments.Should().ContainSingle().Which.Id.Should().Be(reference.Id);
    }

    [Fact]
    public async Task GetByIdWithAttachmentsAsync_WhenBriefingHasSeveralAttachments_ReturnsThemNewestFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var briefing = NewBriefing(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var uploaderId = Guid.NewGuid();

        var first = briefing.AddAttachment("primeiro.pdf", "key-1", 10, uploaderId);
        // UploadedAt is stamped by the entity from the clock, so a second file needs a distinguishable moment.
        await Task.Delay(10);
        var second = briefing.AddAttachment("segundo.pdf", "key-2", 10, uploaderId);

        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByIdWithAttachmentsAsync(briefing.Id);

        // Assert
        result!.Attachments.Select(a => a.Id).Should().ContainInOrder(second.Id, first.Id);
    }

    [Fact]
    public async Task GetByIdWithAttachmentsAsync_WhenBriefingDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetByIdWithAttachmentsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDeliveriesByTaskAsync_WhenTaskHasNoDeliveries_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingRepository(context);

        // Act
        var result = await repository.GetDeliveriesByTaskAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// <c>Attachment.UploadedAt</c> is stamped by the entity itself, so two files created in the same tick
    /// cannot express an order. Setting it directly is what makes the ordering under test observable.
    /// </summary>
    private static Attachment StampDelivery(Attachment attachment, DateTime uploadedAt)
    {
        typeof(Attachment).GetProperty(nameof(Attachment.UploadedAt))!.SetValue(attachment, uploadedAt);

        return attachment;
    }
}
