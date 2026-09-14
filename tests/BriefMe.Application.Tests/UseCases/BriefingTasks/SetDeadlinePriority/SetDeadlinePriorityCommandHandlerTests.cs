using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.SetDeadlinePriority;

public class SetDeadlinePriorityCommandHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly SetDeadlinePriorityCommandHandler _handler;

    public SetDeadlinePriorityCommandHandlerTests()
    {
        _handler = new SetDeadlinePriorityCommandHandler(
            _taskRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
    }

    private BriefingTask ArrangeTask(BriefingTaskStatus? status = null, Guid? designerId = null)
    {
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium, designerId);

        if (status is not null && status != BriefingTaskStatus.NotStarted)
            typeof(BriefingTask).GetProperty(nameof(BriefingTask.Status))!.SetValue(task, status);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    [Fact]
    public async Task Handle_WhenTaskExists_SetsDeadlineAndPriority()
    {
        // Arrange
        var task = ArrangeTask();
        var deadline = Today.AddDays(7);

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, deadline, Priority.Urgent),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Deadline.Should().Be(deadline);
        result.Value.Priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public async Task Handle_WhenTaskAlreadyHasADeadline_ReplacesIt()
    {
        // Arrange
        var task = ArrangeTask();
        task.SetDeadlineAndPriority(Today.AddDays(3), Priority.Low);

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, Today.AddDays(10), Priority.High),
            default);

        // Assert
        result.Value.Deadline.Should().Be(Today.AddDays(10));
        result.Value.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public async Task Handle_WhenPlanningSucceeds_LeavesStatusAndAssigneeUnchanged()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        var task = ArrangeTask(designerId: designerId);

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, Today.AddDays(5), Priority.Medium),
            default);

        // Assert
        result.Value.Status.Should().Be(BriefingTaskStatus.NotStarted);
        result.Value.DesignerId.Should().Be(designerId);
    }

    [Fact]
    public async Task Handle_WhenPlanningSucceeds_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, Today.AddDays(5), Priority.Medium),
            default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(Guid.NewGuid(), Today.AddDays(5), Priority.Medium),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(BriefingTaskStatus.Approved);

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, Today.AddDays(5), Priority.Medium),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(
            BriefingTaskErrors.DeadlineAndPriorityChangeNotAllowed(BriefingTaskStatus.Approved));
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_KeepsTheCurrentValues()
    {
        // Arrange
        var task = ArrangeTask(BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, Today.AddDays(5), Priority.Urgent),
            default);

        // Assert
        task.Deadline.Should().BeNull();
        task.Priority.Should().Be(Priority.Medium);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    public async Task Handle_WhenTaskIsNotApproved_SetsDeadlineAndPriority(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status);

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(task.Id, Today.AddDays(2), Priority.High),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Deadline.Should().Be(Today.AddDays(2));
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(
            new SetDeadlinePriorityCommand(Guid.NewGuid(), Today.AddDays(5), Priority.Medium),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be(CommonErrors.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_DoesNotTouchRepositories()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        await _handler.Handle(
            new SetDeadlinePriorityCommand(Guid.NewGuid(), Today.AddDays(5), Priority.Medium),
            default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
