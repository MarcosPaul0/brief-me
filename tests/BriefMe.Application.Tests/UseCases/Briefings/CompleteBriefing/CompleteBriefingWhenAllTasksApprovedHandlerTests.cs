using BriefMe.Application.UseCases.Briefings.CompleteBriefing;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.Services;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.CompleteBriefing;

public class CompleteBriefingWhenAllTasksApprovedHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly CompleteBriefingWhenAllTasksApprovedHandler _handler;

    private Briefing _briefing = null!;

    public CompleteBriefingWhenAllTasksApprovedHandlerTests()
    {
        _handler = new CompleteBriefingWhenAllTasksApprovedHandler(
            _briefingRepositoryMock.Object,
            _taskRepositoryMock.Object,
            // Pure domain service (BRF-035): no I/O to fake, so the real calculation is what runs.
            new BriefingProgressCalculator(),
            _unitOfWorkMock.Object);
    }

    /// <summary>
    /// A briefing under execution — the state it is in once broken down into tasks (RF015) — carrying
    /// <paramref name="statuses"/>, one task per entry.
    /// </summary>
    private Briefing ArrangeBriefing(
        BriefingStatus briefingStatus = BriefingStatus.InProgress,
        params BriefingTaskStatus[] statuses)
    {
        _briefing = Briefing.Create(
            "Campanha de fim de ano",
            Priority.Medium,
            Guid.NewGuid(),
            Guid.NewGuid());

        ForceBriefingStatus(_briefing, briefingStatus);

        var tasks = statuses.Select(status => ArrangeTask(_briefing.Id, status)).ToList();

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(_briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_briefing);

        _taskRepositoryMock
            .Setup(r => r.GetByBriefingIdAsync(_briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        return _briefing;
    }

    private static BriefingTask ArrangeTask(Guid briefingId, BriefingTaskStatus status)
    {
        var task = BriefingTask.Create(
            briefingId,
            "Key visual",
            Priority.Medium,
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));

        foreach (var step in PathTo(status))
            task.UpdateStatus(step);

        return task;
    }

    /// <summary>Legal RF020 route from NotStarted to <paramref name="status"/> — no forced state.</summary>
    private static BriefingTaskStatus[] PathTo(BriefingTaskStatus status) => status switch
    {
        BriefingTaskStatus.NotStarted => [],
        BriefingTaskStatus.InProgress => [BriefingTaskStatus.InProgress],
        BriefingTaskStatus.Blocked => [BriefingTaskStatus.Blocked],
        BriefingTaskStatus.AwaitingTechnicalApproval =>
            [BriefingTaskStatus.InProgress, BriefingTaskStatus.AwaitingTechnicalApproval],
        BriefingTaskStatus.AwaitingRequesterApproval =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.AwaitingRequesterApproval
        ],
        BriefingTaskStatus.Rejected =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.Rejected
        ],
        BriefingTaskStatus.Approved =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.AwaitingRequesterApproval,
            BriefingTaskStatus.Approved
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped task status.")
    };

    private static void ForceBriefingStatus(Briefing briefing, BriefingStatus status)
    {
        typeof(Briefing)
            .GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, status);
    }

    private static TaskApprovedByRequester Event(Guid briefingId) =>
        new(Guid.NewGuid(), briefingId, Guid.NewGuid());

    [Fact]
    public async Task Handle_WhenEveryTaskIsApproved_CompletesBriefing()
    {
        // Arrange
        var briefing = ArrangeBriefing(
            BriefingStatus.InProgress,
            BriefingTaskStatus.Approved,
            BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Completed);
    }

    [Fact]
    public async Task Handle_WhenEveryTaskIsApproved_PersistsTheCompletion()
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.InProgress, BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    public async Task Handle_WhenAnyTaskIsNotApproved_KeepsBriefingInProgress(BriefingTaskStatus pending)
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.InProgress, BriefingTaskStatus.Approved, pending);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenAnyTaskIsNotApproved_DoesNotCommit()
    {
        // Arrange
        var briefing = ArrangeBriefing(
            BriefingStatus.InProgress,
            BriefingTaskStatus.Approved,
            BriefingTaskStatus.InProgress);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The BRF-085 acceptance: 3 of 4 approved, the fourth rejected and then approved — the briefing must
    /// wait for the fourth (RF058).
    /// </summary>
    [Fact]
    public async Task Handle_WhenTheLastTaskIsRejectedAndThenApproved_CompletesOnlyOnTheLastApproval()
    {
        // Arrange
        var briefing = ArrangeBriefing(
            BriefingStatus.InProgress,
            BriefingTaskStatus.Approved,
            BriefingTaskStatus.Approved,
            BriefingTaskStatus.Approved,
            BriefingTaskStatus.Rejected);

        var tasks = await _taskRepositoryMock.Object.GetByBriefingIdAsync(briefing.Id);
        var lastTask = tasks[^1];

        await _handler.Handle(Event(briefing.Id), CancellationToken.None);
        var statusWhileRejected = briefing.Status;

        // The designer corrects the delivery (RF048) and it walks back up to final approval (RF046).
        lastTask.UpdateStatus(BriefingTaskStatus.InProgress);
        lastTask.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
        lastTask.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
        lastTask.UpdateStatus(BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        statusWhileRejected.Should().Be(BriefingStatus.InProgress);
        briefing.Status.Should().Be(BriefingStatus.Completed);
    }

    [Fact]
    public async Task Handle_WhenBriefingHasNoTasks_KeepsBriefingInProgress()
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.InProgress);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenBriefingHasNoTasks_DoesNotCommit()
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.InProgress);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsAlreadyCompleted_DoesNotCommitAgain()
    {
        // Arrange — a redelivered event (BRF-049 dispatches at least once).
        var briefing = ArrangeBriefing(BriefingStatus.Completed, BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Completed);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingIsNotUnderExecution_DoesNotThrowNorChangeStatus(BriefingStatus status)
    {
        // Arrange
        var briefing = ArrangeBriefing(status, BriefingTaskStatus.Approved);

        // Act
        var act = () => _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        briefing.Status.Should().Be(status);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsNotUnderExecution_DoesNotLoadTasks()
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.Cancelled, BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(Event(briefing.Id), CancellationToken.None);

        // Assert — the unpaginated task list is never read for a briefing that cannot complete.
        _taskRepositoryMock.Verify(
            r => r.GetByBriefingIdAsync(briefing.Id, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsNotFound_DoesNotThrowNorCommit()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var act = () => _handler.Handle(Event(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
