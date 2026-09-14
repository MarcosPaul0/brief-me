using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.RegisterComment;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.RegisterComment;

public class RegisterCommentCommandHandlerTests
{
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly Role _designerRole = Role.Create(DomainRoles.Designer, "Execução das tarefas.", isSystemRole: true);
    private readonly Role _managerRole = Role.Create(DomainRoles.GestorDeDesign, "Gestão do design.", isSystemRole: true);
    private readonly Role _adminRole = Role.Create(DomainRoles.Administrador, "Acesso total.", isSystemRole: true);

    private readonly Guid _assigneeId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly RegisterCommentCommandHandler _handler;

    public RegisterCommentCommandHandlerTests()
    {
        _handler = new RegisterCommentCommandHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        // Default caller: the assignee of the arranged task — the common RF021 path.
        _currentUserMock.Setup(u => u.UserId).Returns(_assigneeId.ToString());

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_designerRole, _managerRole, _adminRole]);
    }

    /// <summary>
    /// A task assigned to <paramref name="designerId"/>, walked to <paramref name="status"/> through legal
    /// transitions only, whose parent briefing belongs to <see cref="_requesterId"/>.
    /// </summary>
    private BriefingTask ArrangeTask(
        Guid? designerId = null,
        BriefingTaskStatus status = BriefingTaskStatus.NotStarted)
    {
        var briefing = Briefing.Create(
            "Campanha de fim de ano",
            Priority.Medium,
            Guid.NewGuid(),
            _requesterId);

        var task = BriefingTask.Create(
            briefing.Id,
            "Key visual",
            Priority.Medium,
            designerId ?? _assigneeId,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));

        foreach (var step in PathTo(status))
            task.UpdateStatus(step);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

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
    public async Task Handle_WhenCallerIsTheAssignee_RegistersTheComment()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(
            new RegisterCommentCommand(task.Id, "Primeira versão amanhã."),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Primeira versão amanhã.");
        result.Value.TaskId.Should().Be(task.Id);
    }

    [Fact]
    public async Task Handle_WhenCommentIsRegistered_AttachesItToTheTask()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        task.Comments.Should().ContainSingle(c => c.Id == result.Value.Id);
    }

    [Fact]
    public async Task Handle_WhenCommentIsRegistered_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCommentIsRegistered_TakesTheAuthorFromTheAuthenticatedUser()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        result.Value.AuthorId.Should().Be(_assigneeId);
    }

    [Fact]
    public async Task Handle_WhenCommentIsRegistered_StampsTheCreationMoment()
    {
        // Arrange
        var task = ArrangeTask();
        var before = DateTime.UtcNow;

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        result.Value.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WhenCommentIsRegistered_LeavesTheTaskUnchanged()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.InProgress);
        var deadline = task.Deadline;

        // Act
        await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.InProgress);
        task.DesignerId.Should().Be(_assigneeId);
        task.Deadline.Should().Be(deadline);
        task.Priority.Should().Be(Priority.Medium);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    [InlineData(BriefingTaskStatus.Approved)]
    public async Task Handle_WhenTaskIsInAnyStatus_RegistersTheComment(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status: status);

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheAssignee_DoesNotLookUpBriefingOrProfiles()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(new RegisterCommentCommand(task.Id, "Alinhado."), default);

        // Assert
        _briefingRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepositoryMock.Verify(
            r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheBriefingRequester_RegistersTheComment()
    {
        // Arrange
        var task = ArrangeTask();
        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Podemos ajustar a cor?"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorId.Should().Be(_requesterId);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheBriefingRequester_DoesNotLookUpProfiles()
    {
        // Arrange
        var task = ArrangeTask();
        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());

        // Act
        await _handler.Handle(new RegisterCommentCommand(task.Id, "Podemos ajustar a cor?"), default);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsADesignManager_RegistersTheComment()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_managerRole.Id);

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Prazo mantido."), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerIsAnAdministrator_RegistersTheComment()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_adminRole.Id);

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Prazo mantido."), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotInvolved_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_designerRole.Id);

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Opinando de fora."), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotInvolvedInTask);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotInvolved_DoesNotRegisterNorCommit()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_designerRole.Id);

        // Act
        await _handler.Handle(new RegisterCommentCommand(task.Id, "Opinando de fora."), default);

        // Assert
        task.Comments.Should().BeEmpty();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssigneeAndCallerIsTheRequester_RegistersTheComment()
    {
        // Arrange
        var task = ArrangeTask(designerId: Guid.Empty);
        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Alguma previsão?"), default);

        // Assert
        task.DesignerId.Should().BeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenParentBriefingNoLongerExists_FallsBackToTheProfileCheck()
    {
        // Arrange
        var task = ArrangeTask();
        var callerId = ArrangeCaller(_managerRole.Id);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Prazo mantido."), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorId.Should().Be(callerId);
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
        var result = await _handler.Handle(new RegisterCommentCommand(task.Id, "Opinando de fora."), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotInvolvedInTask);
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(Guid.NewGuid(), "Alinhado."), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(new RegisterCommentCommand(Guid.NewGuid(), "Alinhado."), default);

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
        await _handler.Handle(new RegisterCommentCommand(Guid.NewGuid(), "Alinhado."), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
