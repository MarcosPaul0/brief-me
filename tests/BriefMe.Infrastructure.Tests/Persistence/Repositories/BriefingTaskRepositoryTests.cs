using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class BriefingTaskRepositoryTests
{
    private static readonly DateTime BaseDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Builds a task with the auditing stamp the interceptor would apply — no interceptor is wired in these
    /// tests, and <c>CreatedAt</c> drives the ordering under test.
    /// </summary>
    private static BriefingTask NewTask(
        Guid briefingId,
        DateTime createdAt,
        Guid? designerId = null,
        DateOnly? deadline = null,
        Priority priority = Priority.Medium)
    {
        var task = BriefingTask.Create(briefingId, "Task", priority, designerId, deadline);
        task.CreatedAt = createdAt;

        return task;
    }

    [Fact]
    public async Task GetByBriefingIdAsync_WhenBriefingHasThreeTasks_ReturnsTheThree()
    {
        // Arrange
        await using var context = CreateContext();
        var briefingId = Guid.NewGuid();

        context.Tasks.AddRange(
            NewTask(briefingId, BaseDate),
            NewTask(briefingId, BaseDate.AddHours(1)),
            NewTask(briefingId, BaseDate.AddHours(2)));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByBriefingIdAsync(briefingId);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByBriefingIdAsync_WhenOtherBriefingsHaveTasks_ReturnsOnlyRequestedBriefingTasks()
    {
        // Arrange
        await using var context = CreateContext();
        var briefingId = Guid.NewGuid();

        context.Tasks.AddRange(
            NewTask(briefingId, BaseDate),
            NewTask(briefingId, BaseDate.AddHours(1)),
            NewTask(Guid.NewGuid(), BaseDate.AddHours(2)));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByBriefingIdAsync(briefingId);

        // Assert
        result.Should().OnlyContain(t => t.BriefingId == briefingId);
    }

    [Fact]
    public async Task GetByBriefingIdAsync_WhenBriefingHasSeveralTasks_ReturnsOldestFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var briefingId = Guid.NewGuid();
        var first = NewTask(briefingId, BaseDate);
        var second = NewTask(briefingId, BaseDate.AddHours(1));
        var third = NewTask(briefingId, BaseDate.AddHours(2));

        context.Tasks.AddRange(third, first, second);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByBriefingIdAsync(briefingId);

        // Assert
        result.Select(t => t.Id).Should().ContainInOrder(first.Id, second.Id, third.Id);
    }

    [Fact]
    public async Task GetByBriefingIdAsync_WhenBriefingHasNoTasks_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByBriefingIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenDesignerHasTasks_ReturnsOnlyOwnTasks()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        context.Tasks.AddRange(
            NewTask(briefingId, BaseDate, designerId),
            NewTask(briefingId, BaseDate.AddHours(1), designerId),
            NewTask(briefingId, BaseDate.AddHours(2), Guid.NewGuid()),
            NewTask(briefingId, BaseDate.AddHours(3)));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.DesignerId == designerId);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenFilteredByStatus_ReturnsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        var blocked = NewTask(briefingId, BaseDate, designerId);
        blocked.UpdateStatus(BriefingTaskStatus.Blocked);
        var inProgress = NewTask(briefingId, BaseDate.AddHours(1), designerId);
        inProgress.UpdateStatus(BriefingTaskStatus.InProgress);

        context.Tasks.AddRange(blocked, inProgress, NewTask(briefingId, BaseDate.AddHours(2), designerId));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(Status: BriefingTaskStatus.Blocked),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(blocked.Id);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenFilteredByPriority_ReturnsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        context.Tasks.AddRange(
            NewTask(briefingId, BaseDate, designerId, priority: Priority.Urgent),
            NewTask(briefingId, BaseDate.AddHours(1), designerId, priority: Priority.Low),
            NewTask(briefingId, BaseDate.AddHours(2), designerId, priority: Priority.Low));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(Priority: Priority.Low),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().OnlyContain(t => t.Priority == Priority.Low).And.HaveCount(2);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenFilteredByStatusAndPriority_ReturnsOnlyTasksMatchingBoth()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        var match = NewTask(briefingId, BaseDate, designerId, priority: Priority.Urgent);
        match.UpdateStatus(BriefingTaskStatus.InProgress);
        var rightStatusWrongPriority = NewTask(briefingId, BaseDate.AddHours(1), designerId, priority: Priority.Low);
        rightStatusWrongPriority.UpdateStatus(BriefingTaskStatus.InProgress);
        var rightPriorityWrongStatus = NewTask(briefingId, BaseDate.AddHours(2), designerId, priority: Priority.Urgent);

        context.Tasks.AddRange(match, rightStatusWrongPriority, rightPriorityWrongStatus);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(Status: BriefingTaskStatus.InProgress, Priority: Priority.Urgent),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(match.Id);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenTasksHaveDeadlines_ReturnsNearestDeadlineFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        var latest = NewTask(briefingId, BaseDate, designerId, new DateOnly(2026, 3, 10));
        var soonest = NewTask(briefingId, BaseDate.AddHours(1), designerId, new DateOnly(2026, 1, 10));
        var middle = NewTask(briefingId, BaseDate.AddHours(2), designerId, new DateOnly(2026, 2, 10));

        context.Tasks.AddRange(latest, soonest, middle);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(),
            page: 1,
            pageSize: 10);

        // Assert
        result.Select(t => t.Id).Should().ContainInOrder(soonest.Id, middle.Id, latest.Id);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenSomeTasksHaveNoDeadline_ReturnsUndatedTasksLast()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        var undated = NewTask(briefingId, BaseDate, designerId);
        var dated = NewTask(briefingId, BaseDate.AddHours(1), designerId, new DateOnly(2026, 6, 30));

        context.Tasks.AddRange(undated, dated);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(),
            page: 1,
            pageSize: 10);

        // Assert
        result.Select(t => t.Id).Should().ContainInOrder(dated.Id, undated.Id);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenDeadlinesTie_BreaksTieByCreatedAt()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();
        var deadline = new DateOnly(2026, 5, 20);

        var earlier = NewTask(briefingId, BaseDate, designerId, deadline);
        var later = NewTask(briefingId, BaseDate.AddHours(1), designerId, deadline);

        context.Tasks.AddRange(later, earlier);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(),
            page: 1,
            pageSize: 10);

        // Assert
        result.Select(t => t.Id).Should().ContainInOrder(earlier.Id, later.Id);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenPaged_ReturnsRequestedSlice()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        var first = NewTask(briefingId, BaseDate, designerId, new DateOnly(2026, 1, 10));
        var second = NewTask(briefingId, BaseDate.AddHours(1), designerId, new DateOnly(2026, 2, 10));
        var third = NewTask(briefingId, BaseDate.AddHours(2), designerId, new DateOnly(2026, 3, 10));

        context.Tasks.AddRange(first, second, third);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            designerId,
            new BriefingTaskFilter(),
            page: 2,
            pageSize: 2);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(third.Id);
    }

    [Fact]
    public async Task CountByDesignerAsync_WhenResultIsPaged_CountsEveryMatchNotJustThePage()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        context.Tasks.AddRange(
            NewTask(briefingId, BaseDate, designerId),
            NewTask(briefingId, BaseDate.AddHours(1), designerId),
            NewTask(briefingId, BaseDate.AddHours(2), designerId),
            NewTask(briefingId, BaseDate.AddHours(3), Guid.NewGuid()));
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var totalCount = await repository.CountByDesignerAsync(designerId, new BriefingTaskFilter());

        // Assert
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task CountByDesignerAsync_WhenFilteredByStatusAndPriority_CountsOnlyTasksMatchingBoth()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        var briefingId = Guid.NewGuid();

        var match = NewTask(briefingId, BaseDate, designerId, priority: Priority.Urgent);
        match.UpdateStatus(BriefingTaskStatus.InProgress);
        var rightStatusWrongPriority = NewTask(briefingId, BaseDate.AddHours(1), designerId, priority: Priority.Low);
        rightStatusWrongPriority.UpdateStatus(BriefingTaskStatus.InProgress);
        var rightPriorityWrongStatus = NewTask(briefingId, BaseDate.AddHours(2), designerId, priority: Priority.Urgent);

        context.Tasks.AddRange(match, rightStatusWrongPriority, rightPriorityWrongStatus);
        await context.SaveChangesAsync();

        var repository = new BriefingTaskRepository(context);

        // Act
        var totalCount = await repository.CountByDesignerAsync(
            designerId,
            new BriefingTaskFilter(Status: BriefingTaskStatus.InProgress, Priority: Priority.Urgent));

        // Assert
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByDesignerAsync_WhenDesignerHasNoTasks_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByDesignerAsync(
            Guid.NewGuid(),
            new BriefingTaskFilter(),
            page: 1,
            pageSize: 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsTask()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingTaskRepository(context);
        var task = NewTask(Guid.NewGuid(), BaseDate);

        // Act
        await repository.AddAsync(task);
        await context.SaveChangesAsync();
        var fetched = await repository.GetByIdAsync(task.Id);

        // Assert
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(task.Id);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsStatusPriorityDeadlineAndDesigner()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingTaskRepository(context);
        var designerId = Guid.NewGuid();
        var deadline = new DateOnly(2026, 9, 15);
        var task = NewTask(Guid.NewGuid(), BaseDate, designerId, deadline, Priority.Urgent);
        task.UpdateStatus(BriefingTaskStatus.InProgress);

        // Act
        await repository.AddAsync(task);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var fetched = await repository.GetByIdAsync(task.Id);

        // Assert
        fetched!.Status.Should().Be(BriefingTaskStatus.InProgress);
        fetched.Priority.Should().Be(Priority.Urgent);
        fetched.Deadline.Should().Be(deadline);
        fetched.DesignerId.Should().Be(designerId);
    }

    [Fact]
    public async Task GetByIdWithCommentsAsync_WhenTaskHasComments_ReturnsThemOldestFirst()
    {
        // Arrange
        await using var context = CreateContext();
        var task = NewTask(Guid.NewGuid(), BaseDate);
        var authorId = Guid.NewGuid();
        StampComment(task.AddComment(authorId, "Segunda"), BaseDate.AddHours(2));
        StampComment(task.AddComment(authorId, "Primeira"), BaseDate);
        StampComment(task.AddComment(authorId, "Terceira"), BaseDate.AddHours(3));

        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByIdWithCommentsAsync(task.Id);

        // Assert
        result!.Comments.Select(c => c.Text).Should().ContainInOrder("Primeira", "Segunda", "Terceira");
    }

    [Fact]
    public async Task GetByIdWithCommentsAsync_WhenIdDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingTaskRepository(context);

        // Act
        var result = await repository.GetByIdWithCommentsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// <c>Comment.CreatedAt</c> is stamped by the entity itself, so three comments created in the same tick
    /// cannot express an order. Setting it directly is what makes the ordering under test observable.
    /// </summary>
    private static void StampComment(Comment comment, DateTime createdAt) =>
        typeof(Comment).GetProperty(nameof(Comment.CreatedAt))!.SetValue(comment, createdAt);

    [Fact]
    public async Task GetByIdAsync_WhenIdDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new BriefingTaskRepository(context);

        // Act
        var fetched = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        fetched.Should().BeNull();
    }
}
