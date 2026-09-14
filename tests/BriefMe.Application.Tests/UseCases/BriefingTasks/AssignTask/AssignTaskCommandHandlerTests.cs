using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.AssignTask;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.AssignTask;

public class AssignTaskCommandHandlerTests
{
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Role _designerRole = Role.Create(DomainRoles.Designer, "Execução das tarefas.", isSystemRole: true);
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly AssignTaskCommandHandler _handler;

    public AssignTaskCommandHandlerTests()
    {
        _handler = new AssignTaskCommandHandler(
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

    private static BriefingTask NewTask(Guid? designerId = null) =>
        BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium, designerId);

    /// <summary>Designer walked to <paramref name="status"/> through the entity's own methods.</summary>
    private User NewDesigner(bool withDesignerRole = true, UserStatus status = UserStatus.Active)
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");

        if (withDesignerRole)
            user.AssignRole(_designerRole.Id);

        if (status is UserStatus.Inactive)
            user.Deactivate();

        return user;
    }

    private BriefingTask ArrangeTask(Guid? designerId = null, BriefingTaskStatus? status = null)
    {
        var task = NewTask(designerId);

        if (status is not null && status != BriefingTaskStatus.NotStarted)
            typeof(BriefingTask).GetProperty(nameof(BriefingTask.Status))!.SetValue(task, status);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        return task;
    }

    private User ArrangeDesigner(bool withDesignerRole = true, UserStatus status = UserStatus.Active)
    {
        var designer = NewDesigner(withDesignerRole, status);

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(designer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(designer);

        return designer;
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssignee_AssignsTheDesigner()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DesignerId.Should().Be(designer.Id);
    }

    [Fact]
    public async Task Handle_WhenAssignmentSucceeds_LeavesTaskStatusUnchanged()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Value.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Fact]
    public async Task Handle_WhenAssignmentSucceeds_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner();

        // Act
        await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAssignmentSucceeds_RaisesTaskAssignedWithTheAssigner()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner();

        // Act
        await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new TaskAssigned(task.Id, task.BriefingId, designer.Id, _managerId));
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenDesignerDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var task = ArrangeTask();
        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, Guid.NewGuid()), default);

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
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, user.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.DesignerRoleRequired);
    }

    [Fact]
    public async Task Handle_WhenTargetUserIsNotADesigner_DoesNotAssign()
    {
        // Arrange
        var task = ArrangeTask();
        var user = ArrangeDesigner(withDesignerRole: false);

        // Act
        await _handler.Handle(new AssignTaskCommand(task.Id, user.Id), default);

        // Assert
        task.DesignerId.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDesignerIsInactive_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask();
        var designer = ArrangeDesigner(status: UserStatus.Inactive);

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

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
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.DesignerRoleNotConfigured);
    }

    [Fact]
    public async Task Handle_WhenTaskIsAlreadyAssigned_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(designerId: Guid.NewGuid());
        var designer = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.AlreadyAssigned);
    }

    [Fact]
    public async Task Handle_WhenTaskIsAlreadyAssigned_KeepsTheCurrentAssignee()
    {
        // Arrange
        var currentDesignerId = Guid.NewGuid();
        var task = ArrangeTask(designerId: currentDesignerId);
        var designer = ArrangeDesigner();

        // Act
        await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        task.DesignerId.Should().Be(currentDesignerId);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskIsAssignedToTheSameDesigner_ReturnsConflict()
    {
        // Arrange
        var designer = ArrangeDesigner();
        var task = ArrangeTask(designerId: designer.Id);

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.AlreadyAssigned);
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.Approved);
        var designer = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.AssignmentNotAllowed(BriefingTaskStatus.Approved));
    }

    [Theory]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    public async Task Handle_WhenTaskIsNotApproved_AssignsTheDesigner(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status: status);
        var designer = ArrangeDesigner();

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(task.Id, designer.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DesignerId.Should().Be(designer.Id);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(new AssignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), default);

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
        await _handler.Handle(new AssignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
