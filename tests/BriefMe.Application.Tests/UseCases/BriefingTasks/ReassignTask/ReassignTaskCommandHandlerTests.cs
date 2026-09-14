using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.ReassignTask;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.ReassignTask;

public class ReassignTaskCommandHandlerTests
{
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Role _designerRole = Role.Create(DomainRoles.Designer, "Execução das tarefas.", isSystemRole: true);
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly ReassignTaskCommandHandler _handler;

    public ReassignTaskCommandHandlerTests()
    {
        _handler = new ReassignTaskCommandHandler(
            _taskRepositoryMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_managerId.ToString());

        _roleRepositoryMock
            .Setup(r => r.GetByNameAsync(DomainRoles.Designer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_designerRole);
    }

    /// <summary>
    /// A task already assigned to <paramref name="designerId"/> (null means the current assignee is a
    /// stranger), registered on the repository. The assignee is carried by <c>Create</c> rather than by
    /// <c>AssignTo</c>, so the arrangement leaves no <c>TaskAssigned</c> behind.
    /// </summary>
    private BriefingTask ArrangeTask(Guid? designerId = null, BriefingTaskStatus? status = null)
    {
        var task = BriefingTask.Create(
            Guid.NewGuid(),
            "Key visual",
            Priority.Medium,
            designerId ?? Guid.NewGuid());

        if (status is not null && status != BriefingTaskStatus.NotStarted)
            typeof(BriefingTask).GetProperty(nameof(BriefingTask.Status))!.SetValue(task, status);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    /// <summary>An unassigned task — the state reassignment refuses, since RF017 owns it.</summary>
    private BriefingTask ArrangeUnassignedTask()
    {
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    private User ArrangeDesigner(bool withDesignerRole = true, UserStatus status = UserStatus.Active)
    {
        var designer = User.Create("Bruno Costa", Email.Create("bruno@briefme.com").Value, "hashed-password");

        if (withDesignerRole)
            designer.AssignRole(_designerRole.Id);

        if (status is UserStatus.Inactive)
            designer.Deactivate();

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(designer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(designer);

        return designer;
    }

    [Fact]
    public async Task Handle_WhenTaskIsAssignedToAnotherDesigner_ReplacesTheAssignee()
    {
        // Arrange
        var task = ArrangeTask();
        var newDesigner = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DesignerId.Should().Be(newDesigner.Id);
    }

    [Fact]
    public async Task Handle_WhenReassignmentSucceeds_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();
        var newDesigner = ArrangeDesigner();

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenReassignmentSucceeds_RaisesTaskAssignedForTheNewDesigner()
    {
        // Arrange
        var task = ArrangeTask();
        var newDesigner = ArrangeDesigner();

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        task.DomainEvents.OfType<TaskAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new TaskAssigned(task.Id, task.BriefingId, newDesigner.Id, _managerId));
    }

    [Fact]
    public async Task Handle_WhenReassignmentSucceeds_LeavesStatusAndPlanningUnchanged()
    {
        // Arrange
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);
        var task = ArrangeTask(status: BriefingTaskStatus.InProgress);
        task.SetDeadlineAndPriority(deadline, Priority.Urgent);
        var newDesigner = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        result.Value.Status.Should().Be(BriefingTaskStatus.InProgress);
        result.Value.Deadline.Should().Be(deadline);
        result.Value.Priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public async Task Handle_WhenReassignmentSucceeds_DoesNotReadSiblingTasks()
    {
        // Arrange
        var task = ArrangeTask();
        var newDesigner = ArrangeDesigner();

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

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
    public async Task Handle_WhenTaskIsNotApproved_ReplacesTheAssignee(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status: status);
        var newDesigner = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DesignerId.Should().Be(newDesigner.Id);
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.Approved);
        var newDesigner = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.ReassignmentNotAllowed(BriefingTaskStatus.Approved));
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_KeepsTheCurrentAssignee()
    {
        // Arrange
        var currentDesignerId = Guid.NewGuid();
        var task = ArrangeTask(currentDesignerId, BriefingTaskStatus.Approved);
        var newDesigner = ArrangeDesigner();

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        task.DesignerId.Should().Be(currentDesignerId);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssignee_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeUnassignedTask();
        var newDesigner = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.NotAssigned);
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssignee_DoesNotAssign()
    {
        // Arrange
        var task = ArrangeUnassignedTask();
        var newDesigner = ArrangeDesigner();

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, newDesigner.Id), default);

        // Assert
        task.DesignerId.Should().BeNull();
        task.DomainEvents.Should().BeEmpty();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetIsTheCurrentDesigner_ReturnsConflict()
    {
        // Arrange
        var designer = ArrangeDesigner();
        var task = ArrangeTask(designer.Id);

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.AlreadyAssignedToDesigner);
    }

    [Fact]
    public async Task Handle_WhenTargetIsTheCurrentDesigner_RaisesNoEvent()
    {
        // Arrange
        var designer = ArrangeDesigner();
        var task = ArrangeTask(designer.Id);

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, designer.Id), default);

        // Assert
        task.DomainEvents.Should().BeEmpty();
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
        var result = await _handler.Handle(new ReassignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTargetDesignerDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var task = ArrangeTask();
        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.DesignerNotFound);
    }

    [Fact]
    public async Task Handle_WhenTargetUserIsNotADesigner_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask();
        var user = ArrangeDesigner(withDesignerRole: false);

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, user.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.DesignerRoleRequired);
    }

    [Fact]
    public async Task Handle_WhenTargetUserIsNotADesigner_KeepsTheCurrentAssignee()
    {
        // Arrange
        var currentDesignerId = Guid.NewGuid();
        var task = ArrangeTask(currentDesignerId);
        var user = ArrangeDesigner(withDesignerRole: false);

        // Act
        await _handler.Handle(new ReassignTaskCommand(task.Id, user.Id), default);

        // Assert
        task.DesignerId.Should().Be(currentDesignerId);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetDesignerIsInactive_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner(status: UserStatus.Inactive);

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.DesignerInactive);
    }

    [Fact]
    public async Task Handle_WhenDesignerRoleIsNotSeeded_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner();
        _roleRepositoryMock
            .Setup(r => r.GetByNameAsync(DomainRoles.Designer, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.DesignerRoleNotConfigured);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(new ReassignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), default);

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
        await _handler.Handle(new ReassignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
