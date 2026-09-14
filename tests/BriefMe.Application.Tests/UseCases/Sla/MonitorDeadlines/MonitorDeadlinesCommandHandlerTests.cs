using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Application.UseCases.Sla.MonitorDeadlines;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.Services;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Sla.MonitorDeadlines;

/// <summary>
/// Pins one pass of the deadline monitor (RF028, BRF-043): what it marks, what it leaves alone, when it
/// commits and when it publishes.
/// </summary>
public class MonitorDeadlinesCommandHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid VideoCategoryId = Guid.NewGuid();

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<ISlaRuleRepository> _slaRuleRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly MonitorDeadlinesCommandHandler _handler;

    public MonitorDeadlinesCommandHandlerTests()
    {
        _handler = new MonitorDeadlinesCommandHandler(
            _briefingRepositoryMock.Object,
            _taskRepositoryMock.Object,
            _slaRuleRepositoryMock.Object,
            // Pure domain service (BRF-043): no I/O to fake, so the real resolution is what runs.
            new SlaDeadlineResolver(),
            _unitOfWorkMock.Object,
            new FixedTimeProvider(NowUtc));

        // Default: an empty system — no policy, no open demand. Each test arranges only what it needs.
        _slaRuleRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _briefingRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _unitOfWorkMock
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // --- Arrangement -------------------------------------------------------------------------------------

    private static Briefing NewBriefing(
        BriefingStatus status = BriefingStatus.InProgress,
        DateOnly? desiredDeliveryDate = null,
        DateTime? reviewedAt = null,
        Priority priority = Priority.Medium)
    {
        var briefing = Briefing.Create(
            "Campanha de fim de ano",
            priority,
            VideoCategoryId,
            Guid.NewGuid(),
            desiredDeliveryDate: desiredDeliveryDate);

        Force(briefing, nameof(Briefing.Status), status);

        if (reviewedAt is not null)
            Force(briefing, nameof(Briefing.ReviewedAt), reviewedAt);

        return briefing;
    }

    private static BriefingTask NewTask(
        Guid briefingId,
        DateOnly? deadline = null,
        Priority priority = Priority.Medium,
        DateTime? createdAt = null)
    {
        var task = BriefingTask.Create(briefingId, "Key visual", priority, Guid.NewGuid(), deadline);
        task.CreatedAt = createdAt ?? NowUtc.AddDays(-1);
        return task;
    }

    private static void Force(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.SetValue(entity, value);

    /// <summary>Puts <paramref name="tasks"/> on the first page of candidates, with their briefings loadable.</summary>
    private void ArrangeTasks(Briefing briefing, params BriefingTask[] tasks)
    {
        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([briefing]);
    }

    private void ArrangeBriefings(params Briefing[] briefings) =>
        _briefingRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefings);

    private void ArrangeRules(params SlaRule[] rules) =>
        _slaRuleRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

    // --- Tasks -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenTaskDeadlineHasPassed_MarksItOverdueAtTheEvaluationInstant()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id, deadline: DateOnly.FromDateTime(NowUtc).AddDays(-2));
        ArrangeTasks(briefing, task);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        task.OverdueSince.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Handle_WhenTaskDeadlineHasPassed_ReportsItInTheRunSummary()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id, deadline: DateOnly.FromDateTime(NowUtc).AddDays(-2));
        ArrangeTasks(briefing, task);

        // Act
        var result = await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new
        {
            EvaluatedAtUtc = NowUtc,
            TasksScanned = 1,
            TasksMarkedOverdue = 1,
            BriefingsScanned = 0,
            BriefingsMarkedOverdue = 0
        });
    }

    [Fact]
    public async Task Handle_WhenTaskDeadlineIsToday_LeavesItOnTime()
    {
        // Arrange
        // A date runs to the end of its day — the same reading SetDeadlineAndPriority applies.
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id, deadline: DateOnly.FromDateTime(NowUtc));
        ArrangeTasks(briefing, task);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTaskCarriesNoDeadlineAndNoRuleApplies_LeavesItOnTime()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id);
        ArrangeTasks(briefing, task);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTheSlaExpiresBeforeTheTypedDeadline_MarksTheTaskByTheSla()
    {
        // Arrange
        // Typed deadline a week out, SLA of 4h since creation 24h ago: the SLA is the one that broke.
        var briefing = NewBriefing();
        var task = NewTask(
            briefing.Id,
            deadline: DateOnly.FromDateTime(NowUtc).AddDays(7),
            priority: Priority.Urgent,
            createdAt: NowUtc.AddHours(-24));
        ArrangeTasks(briefing, task);
        ArrangeRules(SlaRule.Create(VideoCategoryId, Priority.Urgent, 4));

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        task.DomainEvents.OfType<DeadlineOverdue>().Single().DueAtUtc.Should().Be(NowUtc.AddHours(-20));
    }

    [Fact]
    public async Task Handle_WhenTheTaskPriorityHasItsOwnRule_ResolvesTheSlaByThatPriority()
    {
        // Arrange
        // The briefing is Medium; the task was raised to Urgent (RF019), and only the urgent rule is short
        // enough to have expired.
        var briefing = NewBriefing(priority: Priority.Medium);
        var task = NewTask(briefing.Id, priority: Priority.Urgent, createdAt: NowUtc.AddHours(-10));
        ArrangeTasks(briefing, task);
        ArrangeRules(
            SlaRule.Create(null, null, 720),
            SlaRule.Create(VideoCategoryId, Priority.Urgent, 2));

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheTasksBriefingCannotBeLoaded_SkipsItWithoutFailing()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid(), deadline: DateOnly.FromDateTime(NowUtc).AddDays(-5));
        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([task]);

        // Act
        var result = await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.IsOverdue.Should().BeFalse();
    }

    // --- Briefings ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenBriefingDesiredDeliveryDateHasPassed_MarksItOverdue()
    {
        // Arrange
        var briefing = NewBriefing(desiredDeliveryDate: DateOnly.FromDateTime(NowUtc).AddDays(-3));
        ArrangeBriefings(briefing);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        briefing.OverdueSince.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsUnderExecutionAndItsSlaExpired_MarksItByTheSla()
    {
        // Arrange
        // Approved 30h ago under a 6h SLA; no requested date at all.
        var briefing = NewBriefing(
            BriefingStatus.InProgress,
            reviewedAt: NowUtc.AddHours(-30),
            priority: Priority.Urgent);
        ArrangeBriefings(briefing);
        ArrangeRules(SlaRule.Create(null, Priority.Urgent, 6));

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        briefing.DomainEvents.OfType<DeadlineOverdue>().Single().DueAtUtc.Should().Be(NowUtc.AddHours(-24));
    }

    [Fact]
    public async Task Handle_WhenBriefingIsBackWithTheRequester_IgnoresTheSlaClock()
    {
        // Arrange
        // ChangesRequested also fills ReviewedAt, but that instant records a hand-back, not an acceptance —
        // the sector's SLA is not running.
        var briefing = NewBriefing(
            BriefingStatus.ChangesRequested,
            reviewedAt: NowUtc.AddHours(-30),
            priority: Priority.Urgent);
        ArrangeBriefings(briefing);
        ArrangeRules(SlaRule.Create(null, Priority.Urgent, 6));

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        briefing.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenBriefingDesiredDeliveryDateIsAhead_LeavesItOnTime()
    {
        // Arrange
        var briefing = NewBriefing(desiredDeliveryDate: DateOnly.FromDateTime(NowUtc).AddDays(3));
        ArrangeBriefings(briefing);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        briefing.IsOverdue.Should().BeFalse();
    }

    // --- Commit and dispatch -----------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenNothingIsOverdue_DoesNotCommit()
    {
        // Arrange
        var briefing = NewBriefing(desiredDeliveryDate: DateOnly.FromDateTime(NowUtc).AddDays(3));
        ArrangeBriefings(briefing);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNothingIsOverdue_RaisesNoEvent()
    {
        // Arrange
        var briefing = NewBriefing(desiredDeliveryDate: DateOnly.FromDateTime(NowUtc).AddDays(3));
        ArrangeBriefings(briefing);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        briefing.DomainEvents.OfType<DeadlineOverdue>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenSeveralDemandsAreOverdue_CommitsOnce()
    {
        // Arrange
        var briefing = NewBriefing(desiredDeliveryDate: DateOnly.FromDateTime(NowUtc).AddDays(-3));
        ArrangeBriefings(briefing);
        ArrangeTasks(
            briefing,
            NewTask(briefing.Id, deadline: DateOnly.FromDateTime(NowUtc).AddDays(-2)),
            NewTask(briefing.Id, deadline: DateOnly.FromDateTime(NowUtc).AddDays(-1)));

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Since BRF-049 the pass does not publish: it marks and commits, and the commit publishes what the
    /// marked aggregates raised. What this handler owes is the pending event on the aggregate it committed —
    /// the dispatch itself is pinned by <c>UnitOfWorkTests</c>.
    /// </summary>
    [Fact]
    public async Task Handle_WhenSomethingIsOverdue_LeavesTheEventPendingOnTheCommittedAggregate()
    {
        // Arrange
        var briefing = NewBriefing(desiredDeliveryDate: DateOnly.FromDateTime(NowUtc).AddDays(-3));
        ArrangeBriefings(briefing);

        // Act
        await _handler.Handle(new MonitorDeadlinesCommand(), CancellationToken.None);

        // Assert
        briefing.DomainEvents.OfType<DeadlineOverdue>().Single().SubjectId.Should().Be(briefing.Id);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Paging ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenCandidatesSpanSeveralPages_SweepsThemAll()
    {
        // Arrange
        var briefing = NewBriefing();
        var overdue = DateOnly.FromDateTime(NowUtc).AddDays(-2);
        var first = NewTask(briefing.Id, deadline: overdue);
        var second = NewTask(briefing.Id, deadline: overdue);

        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first]);
        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([second]);
        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(3, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([briefing]);

        // Act
        var result = await _handler.Handle(new MonitorDeadlinesCommand(BatchSize: 1), CancellationToken.None);

        // Assert
        result.Value.TasksMarkedOverdue.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenThePassHitsItsCeiling_StopsAndLeavesTheRestForTheNextPass()
    {
        // Arrange
        var briefing = NewBriefing();
        var overdue = DateOnly.FromDateTime(NowUtc).AddDays(-2);
        var second = NewTask(briefing.Id, deadline: overdue);

        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([NewTask(briefing.Id, deadline: overdue)]);
        _taskRepositoryMock
            .Setup(r => r.GetOverdueCandidatesAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([second]);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([briefing]);

        // Act
        await _handler.Handle(
            new MonitorDeadlinesCommand(BatchSize: 1, MaxItemsPerRun: 1),
            CancellationToken.None);

        // Assert
        second.IsOverdue.Should().BeFalse();
    }
}
