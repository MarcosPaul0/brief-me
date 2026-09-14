using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.ReopenTask;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.ReopenTask;

public class ReopenTaskCommandHandlerTests
{
    private static readonly DateOnly Deadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ReopenTaskCommandHandler _handler;

    public ReopenTaskCommandHandlerTests()
    {
        _handler = new ReopenTaskCommandHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
    }

    /// <summary>
    /// A task in <paramref name="taskStatus"/> under a briefing in <paramref name="briefingStatus"/>, both
    /// registered on their repositories. The task is walked to its status through legal RF020 transitions;
    /// only the briefing needs its status forced, since <c>Briefing</c> has no public route to Completed
    /// other than the one BRF-036 drives.
    /// </summary>
    private BriefingTask ArrangeTask(
        BriefingTaskStatus taskStatus = BriefingTaskStatus.Approved,
        BriefingStatus briefingStatus = BriefingStatus.Completed,
        bool briefingExists = true)
    {
        var briefing = Briefing.Create("Campanha de fim de ano", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());
        ForceBriefingStatus(briefing, briefingStatus);

        var task = BriefingTask.Create(briefing.Id, "Key visual", Priority.Urgent, Guid.NewGuid(), Deadline);

        foreach (var step in PathTo(taskStatus))
            task.UpdateStatus(step);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefingExists ? briefing : null);

        Briefing = briefing;

        return task;
    }

    /// <summary>The briefing behind the task arranged last — the other half of what the handler moves.</summary>
    private Briefing Briefing { get; set; } = null!;

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

    private static void ForceBriefingStatus(Briefing briefing, BriefingStatus status) =>
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, status);

    [Fact]
    public async Task Handle_WhenTaskIsApproved_ReturnsTaskInProgress()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenTaskIsReopened_KeepsAssigneeDeadlineAndPriority()
    {
        // Arrange
        var task = ArrangeTask();
        var designerId = task.DesignerId;

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        result.Value.DesignerId.Should().Be(designerId);
        result.Value.Deadline.Should().Be(Deadline);
        result.Value.Priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsCompleted_ReturnsItToInProgress()
    {
        // Arrange
        var task = ArrangeTask(briefingStatus: BriefingStatus.Completed);

        // Act
        await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        Briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsAlreadyInProgress_KeepsItInProgress()
    {
        // Arrange
        var task = ArrangeTask(briefingStatus: BriefingStatus.InProgress);

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenReopeningSucceeds_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenReopeningSucceeds_DoesNotReadSiblingTasks()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByBriefingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public async Task Handle_WhenTaskIsNotApproved_ReturnsConflict(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status, BriefingStatus.InProgress);

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.ReopenNotAllowed(status));
    }

    [Fact]
    public async Task Handle_WhenTaskIsNotApproved_DoesNotCommit()
    {
        // Arrange
        var task = ArrangeTask(BriefingTaskStatus.Blocked, BriefingStatus.InProgress);

        // Act
        await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingCannotReturnToExecution_ReturnsConflict(BriefingStatus status)
    {
        // Arrange
        var task = ArrangeTask(briefingStatus: status);

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingErrors.ReopenNotAllowed(status));
    }

    [Fact]
    public async Task Handle_WhenBriefingCannotReturnToExecution_LeavesTaskApproved()
    {
        // Arrange
        var task = ArrangeTask(briefingStatus: BriefingStatus.Cancelled);

        // Act
        await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.Approved);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenBriefingDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var task = ArrangeTask(briefingExists: false);

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new ReopenTaskCommand(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
