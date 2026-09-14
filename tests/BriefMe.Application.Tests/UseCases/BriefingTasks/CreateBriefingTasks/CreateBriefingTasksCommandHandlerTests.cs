using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.CreateBriefingTasks;

public class CreateBriefingTasksCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly CreateBriefingTasksCommandHandler _handler;

    public CreateBriefingTasksCommandHandlerTests()
    {
        _handler = new CreateBriefingTasksCommandHandler(
            _briefingRepositoryMock.Object,
            _taskRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_managerId.ToString());
    }

    /// <summary>Briefing in Approved, reached through the real transitions (no reflection).</summary>
    private Briefing ApprovedBriefing()
    {
        var briefing = Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        briefing.AddAttachment("brief.pdf", "/storage/brief.pdf", 1024, _requesterId);
        briefing.SubmitForApproval();
        briefing.Approve(_managerId);
        briefing.ClearDomainEvents();

        return briefing;
    }

    private static void ForceStatus(Briefing briefing, BriefingStatus status) =>
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, status);

    private Briefing ArrangeBriefing(BriefingStatus? status = null)
    {
        var briefing = ApprovedBriefing();

        if (status is not null)
            ForceStatus(briefing, status.Value);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private static CreateBriefingTasksCommand CommandFor(Guid briefingId, params CreateBriefingTaskItem[] items) =>
        new(briefingId, items);

    [Fact]
    public async Task Handle_WhenBriefingIsApproved_CreatesOneTaskPerItem()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(
            briefing.Id,
            new CreateBriefingTaskItem("Key visual", Priority.High),
            new CreateBriefingTaskItem("Story assets", Priority.Medium));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenTasksAreCreated_LinksThemToTheBriefing()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Value.Should().OnlyContain(t => t.BriefingId == briefing.Id);
    }

    [Fact]
    public async Task Handle_WhenTasksAreCreated_StartThemAsNotStartedWithoutDesignerOrDeadline()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        var task = result.Value.Single();
        task.Status.Should().Be(BriefingTaskStatus.NotStarted);
        task.DesignerId.Should().BeNull();
        task.Deadline.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenItemsHaveDifferentPriorities_KeepsEachItemsOwnPriority()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(
            briefing.Id,
            new CreateBriefingTaskItem("Key visual", Priority.Urgent),
            new CreateBriefingTaskItem("Story assets", Priority.Low));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Value.Select(t => t.Priority).Should().Equal(Priority.Urgent, Priority.Low);
    }

    [Fact]
    public async Task Handle_WhenBatchIsCreated_ReturnsTasksInTheRequestedOrder()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(
            briefing.Id,
            new CreateBriefingTaskItem("First", Priority.Low),
            new CreateBriefingTaskItem("Second", Priority.Low),
            new CreateBriefingTaskItem("Third", Priority.Low));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Value.Select(t => t.Title).Should().Equal("First", "Second", "Third");
    }

    [Fact]
    public async Task Handle_WhenBatchHasSeveralItems_AddsEachTaskAndCommitsOnce()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(
            briefing.Id,
            new CreateBriefingTaskItem("Key visual", Priority.High),
            new CreateBriefingTaskItem("Story assets", Priority.Medium));

        // Act
        await _handler.Handle(command, default);

        // Assert
        _taskRepositoryMock.Verify(r => r.AddAsync(It.IsAny<BriefingTask>(), default), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFirstTaskIsCreated_MovesBriefingToInProgress()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        await _handler.Handle(command, default);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsAlreadyInProgress_CreatesTheTaskAndKeepsTheStatus()
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.InProgress);
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("Extra deliverable", Priority.Low));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingIsNotApproved_ReturnsConflict(BriefingStatus status)
    {
        // Arrange
        var briefing = ArrangeBriefing(status);
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsNotApproved_KeepsStatusAndCreatesNothing()
    {
        // Arrange
        var briefing = ArrangeBriefing(BriefingStatus.AwaitingApproval);
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        await _handler.Handle(command, default);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
        _taskRepositoryMock.Verify(r => r.AddAsync(It.IsAny<BriefingTask>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Briefing?)null);
        var command = CommandFor(Guid.NewGuid(), new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_DoesNotCommit()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Briefing?)null);
        var command = CommandFor(Guid.NewGuid(), new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        await _handler.Handle(command, default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized(string? userId)
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns(userId);
        var command = CommandFor(Guid.NewGuid(), new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_DoesNotLoadTheRepository()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);
        var command = CommandFor(Guid.NewGuid(), new CreateBriefingTaskItem("Key visual", Priority.High));

        // Act
        await _handler.Handle(command, default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTitleHasSurroundingWhitespace_TrimsIt()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var command = CommandFor(briefing.Id, new CreateBriefingTaskItem("  Key visual  ", Priority.High));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Value.Single().Title.Should().Be("Key visual");
    }
}
