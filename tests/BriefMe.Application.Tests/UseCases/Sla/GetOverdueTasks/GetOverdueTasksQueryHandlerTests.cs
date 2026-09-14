using BriefMe.Application.UseCases.Sla.GetOverdueTasks;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Sla.GetOverdueTasks;

public class GetOverdueTasksQueryHandlerTests
{
    private static readonly DateTime DueAt = new(2026, 7, 20, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime DetectedAt = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly GetOverdueTasksQueryHandler _handler;

    public GetOverdueTasksQueryHandlerTests()
    {
        _handler = new GetOverdueTasksQueryHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _userRepositoryMock.Object);

        // Default: no lookup resolves. The tests that care about names override it.
        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
    }

    private static Briefing NewBriefing(string title) =>
        Briefing.Create(title, Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

    /// <summary>
    /// A task already marked by the deadline monitor (BRF-043) — the only kind this query ever reads.
    /// </summary>
    private static BriefingTask NewOverdueTask(
        Guid briefingId,
        Guid? designerId = null,
        DateOnly? deadline = null,
        string title = "Banner topo")
    {
        var task = BriefingTask.Create(briefingId, title, Priority.High, designerId, deadline);
        task.MarkOverdue(DueAt, DetectedAt);

        return task;
    }

    private static User NewUser(Guid id, string fullName)
    {
        var user = User.Create(fullName, Email.Create("designer@briefme.test").Value, "hash");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(user, id);

        return user;
    }

    [Fact]
    public async Task Handle_WhenTasksAreOverdue_ReturnsThemWithDeadlineAssigneeAndPaginationMetadata()
    {
        // Arrange
        var briefing = NewBriefing("Campanha de verão");
        var designerId = Guid.NewGuid();
        var deadline = new DateOnly(2026, 7, 20);
        var task = NewOverdueTask(briefing.Id, designerId, deadline);

        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(2, 10, default))
            .ReturnsAsync([task]);

        _taskRepositoryMock
            .Setup(r => r.CountOverdueAsync(default))
            .ReturnsAsync(12);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([briefing]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([NewUser(designerId, "Ana Souza")]);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(2, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(12);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(task.Id);
        item.Title.Should().Be("Banner topo");
        item.BriefingId.Should().Be(briefing.Id);
        item.BriefingTitle.Should().Be("Campanha de verão");
        item.Deadline.Should().Be(deadline);
        item.DesignerId.Should().Be(designerId);
        item.DesignerName.Should().Be("Ana Souza");
        item.OverdueSince.Should().Be(DetectedAt);
        item.Priority.Should().Be(Priority.High);
        item.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Fact]
    public async Task Handle_WhenTwoTasksAreOverdue_ReturnsBothInTheRepositoryOrder()
    {
        // Arrange — the "2 overdue tasks" of the BRF-044 acceptance criterion. Ordering is the
        // repository's (oldest mark first) and the handler must not re-sort it.
        var briefingId = Guid.NewGuid();
        var first = NewOverdueTask(briefingId, title: "Banner topo");
        var second = NewOverdueTask(briefingId, title: "Post feed");

        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([first, second]);

        _taskRepositoryMock
            .Setup(r => r.CountOverdueAsync(default))
            .ReturnsAsync(2);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(item => item.Id).Should().ContainInOrder(first.Id, second.Id);
    }

    [Fact]
    public async Task Handle_WhenNothingIsOverdue_ReturnsEmptyPage()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([]);

        _taskRepositoryMock
            .Setup(r => r.CountOverdueAsync(default))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTasksShareBriefings_ResolvesTitlesInOneDistinctLookup()
    {
        // Arrange
        var briefing = NewBriefing("Campanha de verão");
        IReadOnlyCollection<Guid>? requestedIds = null;

        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([NewOverdueTask(briefing.Id), NewOverdueTask(briefing.Id, title: "Post feed")]);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => requestedIds = ids)
            .ReturnsAsync([briefing]);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        requestedIds.Should().ContainSingle().Which.Should().Be(briefing.Id);
        result.Value.Items.Should().OnlyContain(item => item.BriefingTitle == "Campanha de verão");
    }

    [Fact]
    public async Task Handle_WhenTasksShareTheAssignee_ResolvesNamesInOneDistinctLookup()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        IReadOnlyCollection<Guid>? requestedIds = null;

        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync(
            [
                NewOverdueTask(Guid.NewGuid(), designerId),
                NewOverdueTask(Guid.NewGuid(), designerId, title: "Post feed")
            ]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => requestedIds = ids)
            .ReturnsAsync([NewUser(designerId, "Ana Souza")]);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        requestedIds.Should().ContainSingle().Which.Should().Be(designerId);
        result.Value.Items.Should().OnlyContain(item => item.DesignerName == "Ana Souza");
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssignee_LeavesDesignerNullAndSkipsTheUserLookup()
    {
        // Arrange — assignment (RF017) is a separate act, and an unassigned late task is exactly what the
        // Design Manager needs to see.
        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([NewOverdueTask(Guid.NewGuid())]);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.DesignerId.Should().BeNull();
        item.DesignerName.Should().BeNull();
        _userRepositoryMock.Verify(
            r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNamesDoNotResolve_LeavesThemNullAndKeepsTheRow()
    {
        // Arrange — a removed briefing or user degrades one cell, never the list.
        var task = NewOverdueTask(Guid.NewGuid(), Guid.NewGuid());

        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([task]);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(task.Id);
        item.BriefingTitle.Should().BeNull();
        item.DesignerName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoTypedDeadline_ReturnsItWithNullDeadlineAndTheMark()
    {
        // Arrange — a task with no deadline of its own can still breach the SLA policy (RF027); the mark,
        // not the deadline, is what puts it on this list.
        _taskRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([NewOverdueTask(Guid.NewGuid(), Guid.NewGuid())]);

        // Act
        var result = await _handler.Handle(new GetOverdueTasksQuery(1, 20), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Deadline.Should().BeNull();
        item.OverdueSince.Should().Be(DetectedAt);
    }
}
