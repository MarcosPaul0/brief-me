using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class BriefingTaskTests
{
    /// <summary>Same reference the aggregate uses for the RF018 deadline rule.</summary>
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static BriefingTask NewTask(Guid? designerId = null) =>
        BriefingTask.Create(Guid.NewGuid(), "Design the key visual", Priority.Medium, designerId);

    /// <summary>
    /// Walks a task to <paramref name="status"/> through legal transitions only — every RF020 state is
    /// reachable from NotStarted, so no reflection is needed to arrange one. An optional
    /// <paramref name="designerId"/> is carried by <c>Create</c> rather than by <c>AssignTo</c>, so the
    /// arrangement leaves no <c>TaskAssigned</c> behind for the reassignment tests to trip over.
    /// </summary>
    private static BriefingTask TaskIn(BriefingTaskStatus status, Guid? designerId = null)
    {
        var task = NewTask(designerId);

        switch (status)
        {
            case BriefingTaskStatus.NotStarted:
                break;
            case BriefingTaskStatus.InProgress:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                break;
            case BriefingTaskStatus.Blocked:
                task.UpdateStatus(BriefingTaskStatus.Blocked);
                break;
            case BriefingTaskStatus.AwaitingTechnicalApproval:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                break;
            case BriefingTaskStatus.Rejected:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                task.UpdateStatus(BriefingTaskStatus.Rejected);
                break;
            case BriefingTaskStatus.AwaitingRequesterApproval:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                task.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
                break;
            case BriefingTaskStatus.Approved:
                task.UpdateStatus(BriefingTaskStatus.InProgress);
                task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
                task.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
                task.UpdateStatus(BriefingTaskStatus.Approved);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped status.");
        }

        // The walk itself raises EntityChanged on every step (RF034, BRF-049). Arranging a status is not the
        // act under test, so the noise is dropped here and each test observes only what it triggered.
        task.ClearDomainEvents();

        return task;
    }

    [Fact]
    public void Create_WhenDataIsValid_StartsAsNotStarted()
    {
        // Arrange
        var briefingId = Guid.NewGuid();

        // Act
        var task = BriefingTask.Create(briefingId, "  Design the key visual  ", Priority.High);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.NotStarted);
        task.BriefingId.Should().Be(briefingId);
        task.Title.Should().Be("Design the key visual");
        task.Priority.Should().Be(Priority.High);
        task.Comments.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenAssigneeAndDeadlineAreOmitted_LeavesThemUnset()
    {
        // Arrange
        // (none)

        // Act
        var task = NewTask();

        // Assert
        task.DesignerId.Should().BeNull();
        task.Deadline.Should().BeNull();
    }

    [Fact]
    public void Create_WhenAssigneeAndDeadlineAreGiven_KeepsThem()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);

        // Act
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Low, designerId, deadline);

        // Assert
        task.DesignerId.Should().Be(designerId);
        task.Deadline.Should().Be(deadline);
    }

    [Fact]
    public void Create_WhenDesignerIdIsEmptyGuid_LeavesAssigneeNull()
    {
        // Arrange
        // (none)

        // Act
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Low, Guid.Empty);

        // Assert
        task.DesignerId.Should().BeNull();
    }

    [Fact]
    public void Create_WhenBriefingIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => BriefingTask.Create(Guid.Empty, "Key visual", Priority.Low);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenTitleIsNullOrWhitespace_ThrowsDomainException(string? title)
    {
        // Arrange
        // (none)

        // Act
        var act = () => BriefingTask.Create(Guid.NewGuid(), title!, Priority.Low);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Rejected, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Rejected, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.InProgress)]
    public void UpdateStatus_WhenTransitionIsAllowed_ChangesStatus(
        BriefingTaskStatus from,
        BriefingTaskStatus to)
    {
        // Arrange
        var task = TaskIn(from);

        // Act
        task.UpdateStatus(to);

        // Assert
        task.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.Rejected, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.Rejected, BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.AwaitingTechnicalApproval)]
    public void UpdateStatus_WhenTransitionIsNotAllowed_ThrowsDomainException(
        BriefingTaskStatus from,
        BriefingTaskStatus to)
    {
        // Arrange
        var task = TaskIn(from);

        // Act
        var act = () => task.UpdateStatus(to);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateStatus_WhenTransitionIsNotAllowed_LeavesStatusUnchanged()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.NotStarted);

        // Act
        var act = () => task.UpdateStatus(BriefingTaskStatus.Approved);

        // Assert
        act.Should().Throw<DomainException>();
        task.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void UpdateStatus_WhenTargetEqualsCurrentStatus_ThrowsDomainException(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status);

        // Act
        var act = () => task.UpdateStatus(status);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void UpdateStatus_WhenTaskIsApproved_ThrowsDomainException(BriefingTaskStatus to)
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var act = () => task.UpdateStatus(to);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateStatus_WhenStatusIsOutsideTheEnum_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.UpdateStatus((BriefingTaskStatus)99);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateStatus_WhenSiblingTaskChanges_LeavesTheOtherTaskUnchanged()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var first = BriefingTask.Create(briefingId, "Key visual", Priority.Medium);
        var second = BriefingTask.Create(briefingId, "Social cutdowns", Priority.Medium);

        // Act
        first.UpdateStatus(BriefingTaskStatus.InProgress);

        // Assert
        second.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.Rejected, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, BriefingTaskStatus.Approved)]
    public void CanTransitionTo_WhenTransitionIsAllowed_ReturnsTrue(
        BriefingTaskStatus from,
        BriefingTaskStatus to)
    {
        // Arrange
        var task = TaskIn(from);

        // Act
        var canTransition = task.CanTransitionTo(to);

        // Assert
        canTransition.Should().BeTrue();
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.InProgress, BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.AwaitingTechnicalApproval)]
    public void CanTransitionTo_WhenTransitionIsNotAllowed_ReturnsFalse(
        BriefingTaskStatus from,
        BriefingTaskStatus to)
    {
        // Arrange
        var task = TaskIn(from);

        // Act
        var canTransition = task.CanTransitionTo(to);

        // Assert
        canTransition.Should().BeFalse();
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void CanTransitionTo_WhenTargetEqualsCurrentStatus_ReturnsFalse(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status);

        // Act
        var canTransition = task.CanTransitionTo(status);

        // Assert
        canTransition.Should().BeFalse();
    }

    [Theory]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void CanTransitionTo_WhenTaskIsApproved_ReturnsFalse(BriefingTaskStatus to)
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var canTransition = task.CanTransitionTo(to);

        // Assert
        canTransition.Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_WhenStatusIsOutsideTheEnum_ReturnsFalse()
    {
        // Arrange
        var task = NewTask();

        // Act
        var canTransition = task.CanTransitionTo((BriefingTaskStatus)99);

        // Assert
        canTransition.Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_WhenConsulted_LeavesStatusUnchanged()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);

        // Act
        task.CanTransitionTo(BriefingTaskStatus.AwaitingTechnicalApproval);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public void AssignTo_WhenTaskHasNoAssignee_SetsDesignerId()
    {
        // Arrange
        var task = NewTask();
        var designerId = Guid.NewGuid();

        // Act
        task.AssignTo(designerId, Guid.NewGuid());

        // Assert
        task.DesignerId.Should().Be(designerId);
    }

    [Fact]
    public void AssignTo_WhenTaskIsAssigned_RaisesTaskAssigned()
    {
        // Arrange
        var task = NewTask();
        var designerId = Guid.NewGuid();
        var assignedById = Guid.NewGuid();

        // Act
        task.AssignTo(designerId, assignedById);

        // Assert
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new TaskAssigned(task.Id, task.BriefingId, designerId, assignedById));
    }

    [Fact]
    public void AssignTo_WhenTaskIsAssigned_LeavesStatusUnchanged()
    {
        // Arrange
        var task = NewTask();

        // Act
        task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void AssignTo_WhenTaskIsNotApproved_IsAllowed(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status);

        // Act
        task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        task.DesignerId.Should().NotBeNull();
    }

    [Fact]
    public void AssignTo_WhenTaskIsApproved_ThrowsDomainException()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var act = () => task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignTo_WhenTaskIsAlreadyAssigned_ThrowsDomainException()
    {
        // Arrange
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Low, Guid.NewGuid());

        // Act
        var act = () => task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignTo_WhenAssigneeIsTheCurrentOne_ThrowsDomainException()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Low, designerId);

        // Act
        var act = () => task.AssignTo(designerId, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignTo_WhenTaskIsAlreadyAssigned_KeepsTheCurrentAssignee()
    {
        // Arrange
        var currentDesignerId = Guid.NewGuid();
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Low, currentDesignerId);

        // Act
        var act = () => task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        task.DesignerId.Should().Be(currentDesignerId);
    }

    [Fact]
    public void AssignTo_WhenDesignerIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.AssignTo(Guid.Empty, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignTo_WhenAssignerIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.AssignTo(Guid.NewGuid(), Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignTo_WhenAssignmentIsRefused_RaisesNoEvent()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var act = () => task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        task.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReassignTo_WhenTaskIsAssigned_ReplacesTheDesigner()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid());
        var newDesignerId = Guid.NewGuid();

        // Act
        task.ReassignTo(newDesignerId, Guid.NewGuid());

        // Assert
        task.DesignerId.Should().Be(newDesignerId);
    }

    [Fact]
    public void ReassignTo_WhenTaskIsReassigned_RaisesTaskAssignedForTheNewDesigner()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid());
        var newDesignerId = Guid.NewGuid();
        var reassignedById = Guid.NewGuid();

        // Act
        task.ReassignTo(newDesignerId, reassignedById);

        // Assert
        task.DomainEvents.OfType<TaskAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new TaskAssigned(task.Id, task.BriefingId, newDesignerId, reassignedById));
    }

    [Fact]
    public void ReassignTo_WhenTaskIsReassigned_LeavesStatusDeadlineAndPriorityUnchanged()
    {
        // Arrange
        var deadline = Today.AddDays(3);
        var task = TaskIn(BriefingTaskStatus.InProgress, Guid.NewGuid());
        task.SetDeadlineAndPriority(deadline, Priority.Urgent);

        // Act
        task.ReassignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.InProgress);
        task.Deadline.Should().Be(deadline);
        task.Priority.Should().Be(Priority.Urgent);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void ReassignTo_WhenTaskIsNotApproved_IsAllowed(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status, Guid.NewGuid());
        var newDesignerId = Guid.NewGuid();

        // Act
        task.ReassignTo(newDesignerId, Guid.NewGuid());

        // Assert
        task.DesignerId.Should().Be(newDesignerId);
    }

    [Fact]
    public void ReassignTo_WhenTaskIsApproved_ThrowsDomainException()
    {
        // Arrange
        var currentDesignerId = Guid.NewGuid();
        var task = TaskIn(BriefingTaskStatus.Approved, currentDesignerId);

        // Act
        var act = () => task.ReassignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        task.DesignerId.Should().Be(currentDesignerId);
    }

    [Fact]
    public void ReassignTo_WhenTaskHasNoAssignee_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.ReassignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        task.DesignerId.Should().BeNull();
    }

    [Fact]
    public void ReassignTo_WhenTargetIsTheCurrentDesigner_ThrowsDomainException()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        var task = NewTask(designerId);

        // Act
        var act = () => task.ReassignTo(designerId, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReassignTo_WhenDesignerIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid());

        // Act
        var act = () => task.ReassignTo(Guid.Empty, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReassignTo_WhenReassignerIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid());

        // Act
        var act = () => task.ReassignTo(Guid.NewGuid(), Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReassignTo_WhenReassignmentIsRefused_RaisesNoEvent()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved, Guid.NewGuid());

        // Act
        var act = () => task.ReassignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        task.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenDeadlineIsInTheFuture_SetsBothValues()
    {
        // Arrange
        var task = NewTask();
        var deadline = Today.AddDays(7);

        // Act
        task.SetDeadlineAndPriority(deadline, Priority.Urgent);

        // Assert
        task.Deadline.Should().Be(deadline);
        task.Priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenDeadlineIsToday_IsAllowed()
    {
        // Arrange
        var task = NewTask();

        // Act
        task.SetDeadlineAndPriority(Today, Priority.High);

        // Assert
        task.Deadline.Should().Be(Today);
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenTaskAlreadyHasADeadline_ReplacesIt()
    {
        // Arrange
        var task = NewTask();
        task.SetDeadlineAndPriority(Today.AddDays(3), Priority.Low);

        // Act
        task.SetDeadlineAndPriority(Today.AddDays(10), Priority.High);

        // Assert
        task.Deadline.Should().Be(Today.AddDays(10));
        task.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenValuesAreSet_LeavesStatusAndAssigneeUnchanged()
    {
        // Arrange
        var task = NewTask();
        var designerId = Guid.NewGuid();
        task.AssignTo(designerId, Guid.NewGuid());

        // Act
        task.SetDeadlineAndPriority(Today.AddDays(5), Priority.Medium);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.NotStarted);
        task.DesignerId.Should().Be(designerId);
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenDeadlineIsInThePast_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.SetDeadlineAndPriority(Today.AddDays(-1), Priority.Medium);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenDeadlineIsInThePast_KeepsTheCurrentValues()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.SetDeadlineAndPriority(Today.AddDays(-1), Priority.Urgent);

        // Assert
        act.Should().Throw<DomainException>();
        task.Deadline.Should().BeNull();
        task.Priority.Should().Be(Priority.Medium);
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenPriorityIsOutsideTheEnum_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.SetDeadlineAndPriority(Today.AddDays(1), (Priority)99);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    public void SetDeadlineAndPriority_WhenTaskIsNotApproved_IsAllowed(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status);

        // Act
        task.SetDeadlineAndPriority(Today.AddDays(2), Priority.High);

        // Assert
        task.Deadline.Should().Be(Today.AddDays(2));
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenTaskIsApproved_ThrowsDomainException()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var act = () => task.SetDeadlineAndPriority(Today.AddDays(2), Priority.High);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddComment_WhenTextIsValid_AppendsToComments()
    {
        // Arrange
        var task = NewTask();
        var authorId = Guid.NewGuid();

        // Act
        var comment = task.AddComment(authorId, "Waiting on the logo files.");

        // Assert
        task.Comments.Should().ContainSingle().Which.Should().Be(comment);
        comment.TaskId.Should().Be(task.Id);
        comment.AuthorId.Should().Be(authorId);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Rejected)]
    public void AddComment_WhenTaskIsInATerminalOrBlockedStatus_IsStillAllowed(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status);

        // Act
        task.AddComment(Guid.NewGuid(), "Following up on this.");

        // Assert
        task.Comments.Should().ContainSingle();
    }

    [Fact]
    public void AddComment_WhenTextIsBlank_ThrowsDomainException()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.AddComment(Guid.NewGuid(), "   ");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reopen_WhenTaskIsApproved_ChangesStatusToInProgress()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        task.Reopen();

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public void Reopen_WhenTaskIsApproved_KeepsAssigneeDeadlineAndPriority()
    {
        // Arrange
        var deadline = Today.AddDays(5);
        var designerId = Guid.NewGuid();
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Urgent, designerId, deadline);
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
        task.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
        task.UpdateStatus(BriefingTaskStatus.Approved);

        // Act
        task.Reopen();

        // Assert
        task.DesignerId.Should().Be(designerId);
        task.Deadline.Should().Be(deadline);
        task.Priority.Should().Be(Priority.Urgent);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public void Reopen_WhenTaskIsNotApproved_ThrowsDomainException(BriefingTaskStatus status)
    {
        // Arrange
        var task = TaskIn(status);

        // Act
        var act = () => task.Reopen();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reopen_WhenTaskIsNotApproved_LeavesStatusUnchanged()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Blocked);

        // Act
        var act = () => task.Reopen();

        // Assert
        act.Should().Throw<DomainException>();
        task.Status.Should().Be(BriefingTaskStatus.Blocked);
    }

    [Fact]
    public void Reopen_WhenCalledTwice_ThrowsDomainExceptionOnTheSecondCall()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);
        task.Reopen();

        // Act
        var act = () => task.Reopen();

        // Assert
        act.Should().Throw<DomainException>();
    }

    /// <summary>
    /// The reopening edge stays out of the RF020 table so <c>UpdateTaskStatusCommand</c> (BRF-029), which
    /// lets the assignee set <c>InProgress</c>, cannot reach it — RF025 belongs to the Design Manager.
    /// </summary>
    [Fact]
    public void UpdateStatus_WhenTaskIsApprovedAndTargetIsInProgress_ThrowsDomainException()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var act = () => task.UpdateStatus(BriefingTaskStatus.InProgress);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CanTransitionTo_WhenTaskIsApprovedAndTargetIsInProgress_ReturnsFalse()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var canTransition = task.CanTransitionTo(BriefingTaskStatus.InProgress);

        // Assert
        canTransition.Should().BeFalse();
    }

    // --- Overdue marking (RF028, BRF-043) ---------------------------------------------------------------

    private static readonly DateTime DueAtUtc = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DetectedAtUtc = new(2026, 8, 2, 0, 15, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkOverdue_WhenTaskIsOpen_RecordsTheDetectionInstant()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        task.OverdueSince.Should().Be(DetectedAtUtc);
    }

    [Fact]
    public void MarkOverdue_WhenTaskIsOpen_RaisesDeadlineOverdueWithTheAssignee()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        var task = TaskIn(BriefingTaskStatus.InProgress, designerId);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        task.DomainEvents.OfType<DeadlineOverdue>().Single()
            .Should().BeEquivalentTo(new
            {
                Subject = DemandSubject.Task,
                SubjectId = task.Id,
                task.BriefingId,
                ResponsibleId = designerId,
                DueAtUtc,
                DetectedAtUtc
            });
    }

    [Fact]
    public void MarkOverdue_WhenTaskHasNoAssignee_RaisesTheEventWithoutAResponsible()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.NotStarted);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        task.DomainEvents.OfType<DeadlineOverdue>().Single().ResponsibleId.Should().BeNull();
    }

    [Fact]
    public void MarkOverdue_WhenTaskIsAlreadyMarked_KeepsTheFirstDetectionInstant()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc.AddHours(1));

        // Assert
        task.OverdueSince.Should().Be(DetectedAtUtc);
    }

    [Fact]
    public void MarkOverdue_WhenTaskIsAlreadyMarked_DoesNotRaiseTheEventAgain()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc.AddHours(1));

        // Assert
        task.DomainEvents.OfType<DeadlineOverdue>().Should().HaveCount(1);
    }

    [Fact]
    public void MarkOverdue_WhenTaskIsApproved_ThrowsDomainException()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        var act = () => task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkOverdue_WhenTaskIsBlocked_MarksItAnyway()
    {
        // Arrange
        // Blocking does not stop the clock — RF028 monitors the deadline, not the activity.
        var task = TaskIn(BriefingTaskStatus.Blocked);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenTaskWasOverdue_ClearsTheMark()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        task.SetDeadlineAndPriority(Today.AddDays(7), Priority.High);

        // Assert
        task.OverdueSince.Should().BeNull();
    }

    [Fact]
    public void MarkOverdue_WhenTaskWasReplanned_MarksItAgain()
    {
        // Arrange
        // "Exactly once" is per breach, not per lifetime: a replanned task is under watch again.
        var task = TaskIn(BriefingTaskStatus.InProgress);
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);
        task.SetDeadlineAndPriority(Today, Priority.High);
        task.ClearDomainEvents();

        // Act
        task.MarkOverdue(DueAtUtc.AddDays(30), DetectedAtUtc.AddDays(31));

        // Assert
        task.DomainEvents.OfType<DeadlineOverdue>().Should().HaveCount(1);
    }

    [Fact]
    public void IsOverdue_WhenTaskWasNeverMarked_ReturnsFalse()
    {
        // Arrange
        var task = NewTask();

        // Act
        var isOverdue = task.IsOverdue;

        // Assert
        isOverdue.Should().BeFalse();
    }

    // --- Relevant changes (RF034, BRF-049) -----------------------------------------------------------------

    [Fact]
    public void UpdateStatus_WhenStatusMoves_RaisesEntityChanged()
    {
        // Arrange
        var task = NewTask();

        // Act
        task.UpdateStatus(BriefingTaskStatus.InProgress);

        // Assert
        task.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new EntityChanged(
                    DemandSubject.Task,
                    task.Id,
                    task.BriefingId,
                    EntityChangeKind.StatusChanged,
                    null));
    }

    [Fact]
    public void UpdateStatus_WhenTransitionIsRefused_RaisesNoEntityChanged()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.UpdateStatus(BriefingTaskStatus.Approved);

        // Assert
        act.Should().Throw<DomainException>();
        task.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void ReassignTo_WhenTaskIsReassigned_RaisesEntityChangedWithTheAuthor()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid());
        var reassignedById = Guid.NewGuid();

        // Act
        task.ReassignTo(Guid.NewGuid(), reassignedById);

        // Assert
        task.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new EntityChanged(
                    DemandSubject.Task,
                    task.Id,
                    task.BriefingId,
                    EntityChangeKind.Reassigned,
                    reassignedById));
    }

    /// <summary>
    /// First assignment is not a change to something the involved people were already following — it has
    /// <see cref="TaskAssigned"/> (RF033) and nothing else.
    /// </summary>
    [Fact]
    public void AssignTo_WhenTaskIsAssigned_RaisesNoEntityChanged()
    {
        // Arrange
        var task = NewTask();

        // Act
        task.AssignTo(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        task.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenTaskIsPlanned_RaisesEntityChanged()
    {
        // Arrange
        var task = NewTask();

        // Act
        task.SetDeadlineAndPriority(Today.AddDays(5), Priority.Urgent);

        // Assert
        task.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Change.Should().Be(EntityChangeKind.Replanned);
    }

    [Fact]
    public void SetDeadlineAndPriority_WhenDeadlineIsRefused_RaisesNoEntityChanged()
    {
        // Arrange
        var task = NewTask();

        // Act
        var act = () => task.SetDeadlineAndPriority(Today.AddDays(-1), Priority.Urgent);

        // Assert
        act.Should().Throw<DomainException>();
        task.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Reopen_WhenTaskIsReopened_RaisesEntityChanged()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.Approved);

        // Act
        task.Reopen();

        // Assert
        task.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Change.Should().Be(EntityChangeKind.Reopened);
    }

    [Fact]
    public void Reopen_WhenReopeningIsRefused_RaisesNoEntityChanged()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);

        // Act
        var act = () => task.Reopen();

        // Assert
        act.Should().Throw<DomainException>();
        task.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void MarkOverdue_WhenTaskIsMarked_RaisesNoEntityChanged()
    {
        // Arrange
        var task = TaskIn(BriefingTaskStatus.InProgress);

        // Act
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        task.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }
}
