using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.UpdateTaskStatus;

public class UpdateTaskStatusCommandHandlerTests
{
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly Role _designerRole = Role.Create(DomainRoles.Designer, "Execução das tarefas.", isSystemRole: true);
    private readonly Role _managerRole = Role.Create(DomainRoles.GestorDeDesign, "Gestão do design.", isSystemRole: true);
    private readonly Role _adminRole = Role.Create(DomainRoles.Administrador, "Acesso total.", isSystemRole: true);

    private readonly Guid _assigneeId = Guid.NewGuid();
    private readonly UpdateTaskStatusCommandHandler _handler;

    public UpdateTaskStatusCommandHandlerTests()
    {
        _handler = new UpdateTaskStatusCommandHandler(
            _taskRepositoryMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        // Default caller: the assignee of the arranged task — the common RF020 path.
        _currentUserMock.Setup(u => u.UserId).Returns(_assigneeId.ToString());

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_designerRole, _managerRole, _adminRole]);
    }

    /// <summary>
    /// A task assigned to <paramref name="designerId"/>, walked to <paramref name="status"/> through legal
    /// transitions only — every state this suite needs is reachable from NotStarted.
    /// </summary>
    private BriefingTask ArrangeTask(
        Guid? designerId = null,
        BriefingTaskStatus status = BriefingTaskStatus.NotStarted)
    {
        var task = BriefingTask.Create(
            Guid.NewGuid(),
            "Key visual",
            Priority.Medium,
            designerId ?? _assigneeId,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));

        foreach (var step in PathTo(status))
            task.UpdateStatus(step);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    private static BriefingTaskStatus[] PathTo(BriefingTaskStatus status) => status switch
    {
        BriefingTaskStatus.NotStarted => [],
        BriefingTaskStatus.InProgress => [BriefingTaskStatus.InProgress],
        BriefingTaskStatus.Blocked => [BriefingTaskStatus.Blocked],
        BriefingTaskStatus.AwaitingTechnicalApproval =>
            [BriefingTaskStatus.InProgress, BriefingTaskStatus.AwaitingTechnicalApproval],
        BriefingTaskStatus.Rejected =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.Rejected
        ],
        BriefingTaskStatus.AwaitingRequesterApproval =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.AwaitingRequesterApproval
        ],
        BriefingTaskStatus.Approved =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.AwaitingRequesterApproval,
            BriefingTaskStatus.Approved
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped status.")
    };

    /// <summary>Makes the authenticated caller a user holding <paramref name="roleIds"/>.</summary>
    private Guid ArrangeCaller(params Guid[] roleIds)
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");

        foreach (var roleId in roleIds)
            user.AssignRole(roleId);

        _currentUserMock.Setup(u => u.UserId).Returns(user.Id.ToString());

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        return user.Id;
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheAssignee_UpdatesStatus()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenUpdateSucceeds_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUpdateSucceeds_LeavesAssignmentAndPlanningUnchanged()
    {
        // Arrange
        var task = ArrangeTask();
        var deadline = task.Deadline;

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.Value.DesignerId.Should().Be(_assigneeId);
        result.Value.Deadline.Should().Be(deadline);
        result.Value.Priority.Should().Be(Priority.Medium);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheAssignee_DoesNotLookUpProfiles()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress), default);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskChanges_DoesNotLoadSiblingTasks()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByBriefingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted, BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked, BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.Rejected, BriefingTaskStatus.InProgress)]
    public async Task Handle_WhenTransitionIsAnExecutionMove_UpdatesStatus(
        BriefingTaskStatus from,
        BriefingTaskStatus to)
    {
        // Arrange
        var task = ArrangeTask(status: from);

        // Act
        var result = await _handler.Handle(new UpdateTaskStatusCommand(task.Id, to), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(to);
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
            new UpdateTaskStatusCommand(Guid.NewGuid(), BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheAssigneeNorAManager_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_designerRole.Id);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotAssignedToCaller);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheAssigneeNorAManager_LeavesStatusUnchanged()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_designerRole.Id);

        // Act
        await _handler.Handle(new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress), default);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.NotStarted);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsADesignManager_UpdatesStatus()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_managerRole.Id);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenCallerIsAnAdministrator_UpdatesStatus()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_adminRole.Id);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssigneeAndCallerIsNotAManager_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask(designerId: Guid.Empty);
        ArrangeCaller(_designerRole.Id);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        task.DesignerId.Should().BeNull();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenCallerNoLongerExists_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask();
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotAssignedToCaller);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Approved)]
    [InlineData(BriefingTaskStatus.Rejected)]
    public async Task Handle_WhenStatusBelongsToTheApprovalFlow_ReturnsConflict(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.InProgress);

        // Act
        var result = await _handler.Handle(new UpdateTaskStatusCommand(task.Id, status), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.StatusNotSetDirectly(status));
    }

    [Fact]
    public async Task Handle_WhenStatusBelongsToTheApprovalFlow_DoesNotCommit()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.AwaitingRequesterApproval);

        // Act
        await _handler.Handle(new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.Approved), default);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.AwaitingRequesterApproval);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTransitionIsNotAllowed_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.InProgress);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.NotStarted),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.StatusTransitionNotAllowed(
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.NotStarted));
    }

    [Fact]
    public async Task Handle_WhenTargetEqualsCurrentStatus_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.InProgress);

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, BriefingTaskStatus.InProgress),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    public async Task Handle_WhenTaskIsApproved_ReturnsConflict(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.Approved);

        // Act
        var result = await _handler.Handle(new UpdateTaskStatusCommand(task.Id, status), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.StatusTransitionNotAllowed(
            BriefingTaskStatus.Approved,
            status));
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(
            new UpdateTaskStatusCommand(Guid.NewGuid(), BriefingTaskStatus.InProgress),
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
            new UpdateTaskStatusCommand(Guid.NewGuid(), BriefingTaskStatus.InProgress),
            default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
