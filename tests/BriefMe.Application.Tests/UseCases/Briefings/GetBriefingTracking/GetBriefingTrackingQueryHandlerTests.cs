using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.GetBriefingTracking;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.Services;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.Tests.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingTracking;

public class GetBriefingTrackingQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Briefing _briefing = Briefing.Create("Campanha de verão", Priority.High, Guid.NewGuid(), Guid.NewGuid());
    private readonly GetBriefingTrackingQueryHandler _handler;

    public GetBriefingTrackingQueryHandlerTests()
    {
        _handler = new GetBriefingTrackingQueryHandler(
            _briefingRepositoryMock.Object,
            _taskRepositoryMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            new BriefingProgressCalculator(),
            _currentUserMock.Object);

        // Default caller: the requester who owns the briefing, the common path — the access tests override it.
        _currentUserMock.Setup(u => u.UserId).Returns(_briefing.RequesterId.ToString());

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(_briefing.Id, default))
            .ReturnsAsync(_briefing);

        // Default: no tasks and no resolvable names. The tests that care override these.
        _taskRepositoryMock
            .Setup(r => r.GetByBriefingIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
    }

    private static User NewUser(string fullName) =>
        User.Create(fullName, Email.Create($"{Guid.NewGuid():N}@briefme.test").Value, "hash");

    private BriefingTask NewTask(string title = "Banner topo", Guid? designerId = null) =>
        BriefingTask.Create(_briefing.Id, title, Priority.Medium, designerId ?? Guid.NewGuid());

    /// <summary>
    /// Walks the RF020 table to <c>Approved</c> instead of forcing the status: the state machine is the
    /// only way in, and the progress rule under test counts exactly this status.
    /// </summary>
    private static BriefingTask Approve(BriefingTask task)
    {
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        task.UpdateStatus(BriefingTaskStatus.AwaitingTechnicalApproval);
        task.UpdateStatus(BriefingTaskStatus.AwaitingRequesterApproval);
        task.UpdateStatus(BriefingTaskStatus.Approved);

        return task;
    }

    private void GivenTasks(params BriefingTask[] tasks) =>
        _taskRepositoryMock
            .Setup(r => r.GetByBriefingIdAsync(_briefing.Id, default))
            .ReturnsAsync(tasks);

    private void GivenPeople(params User[] people) =>
        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(people);

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(unknownId, default))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(unknownId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotInvolved_ReturnsForbidden()
    {
        // Arrange
        var outsider = NewUser("Estranho");
        _currentUserMock.Setup(u => u.UserId).Returns(outsider.Id.ToString());

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(outsider.Id, default))
            .ReturnsAsync(outsider);

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingErrors.NotInvolvedToTrack);
    }

    [Fact]
    public async Task Handle_WhenCallerHasManagementRole_ReturnsTracking()
    {
        // Arrange
        var manager = NewUser("Marco Gestor");
        var managerRole = Role.Create(DomainRoles.GestorDeDesign, "Gestão", isSystemRole: true);
        manager.AssignRole(managerRole.Id);

        _currentUserMock.Setup(u => u.UserId).Returns(manager.Id.ToString());

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(manager.Id, default))
            .ReturnsAsync(manager);

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(default))
            .ReturnsAsync([managerRole]);

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_briefing.Id);
    }

    [Fact]
    public async Task Handle_WhenHalfOfTheTasksAreApproved_ReturnsFiftyPercent()
    {
        // Arrange — the literal acceptance criterion of BRF-035/BRF-039: 4 tasks, 2 approved.
        GivenTasks(
            Approve(NewTask("Banner topo")),
            Approve(NewTask("Banner lateral")),
            NewTask("Post carrossel"),
            NewTask("Vídeo curto"));

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Progress.TotalTasks.Should().Be(4);
        result.Value.Progress.ApprovedTasks.Should().Be(2);
        result.Value.Progress.Percentage.Should().Be(50m);
        result.Value.Progress.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenEveryTaskIsApproved_ReturnsCompleteProgress()
    {
        // Arrange
        GivenTasks(Approve(NewTask("Banner topo")), Approve(NewTask("Banner lateral")));

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.Value.Progress.Percentage.Should().Be(100m);
        result.Value.Progress.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenBriefingHasNoTasks_ReturnsZeroProgressAndEmptyTimeline()
    {
        // Arrange
        // (the constructor's default: the briefing was approved but not broken down yet)

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Progress.TotalTasks.Should().Be(0);
        result.Value.Progress.Percentage.Should().Be(0m);
        result.Value.Progress.IsComplete.Should().BeFalse();
        result.Value.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenBriefingHasTasks_ReturnsThemInRepositoryOrder()
    {
        // Arrange
        GivenTasks(NewTask("Banner topo"), NewTask("Post carrossel"));

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.Value.Tasks.Select(task => task.Title).Should().ContainInOrder("Banner topo", "Post carrossel");
    }

    [Fact]
    public async Task Handle_WhenTasksHaveAssignees_ResolvesTheirNames()
    {
        // Arrange
        var designer = NewUser("Ana Designer");
        GivenTasks(NewTask("Banner topo", designer.Id));
        GivenPeople(designer);

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.Value.Tasks.Should().ContainSingle().Which.DesignerName.Should().Be("Ana Designer");
    }

    [Fact]
    public async Task Handle_WhenAssigneeNameDoesNotResolve_KeepsTheRowWithoutTheName()
    {
        // Arrange — a soft-deleted designer is filtered out of the batched lookup.
        GivenTasks(NewTask("Banner topo", Guid.NewGuid()));

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        var task = result.Value.Tasks.Should().ContainSingle().Subject;
        task.Title.Should().Be("Banner topo");
        task.DesignerName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssigneeOrDeadline_ReturnsThemAsNull()
    {
        // Arrange — creation gives neither (RF017/RF018 are separate acts).
        GivenTasks(BriefingTask.Create(_briefing.Id, "Banner topo", Priority.Low, Guid.Empty));

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        var task = result.Value.Tasks.Should().ContainSingle().Subject;
        task.DesignerId.Should().BeNull();
        task.DesignerName.Should().BeNull();
        task.Deadline.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheRequester_ReturnsHeaderWithRequesterName()
    {
        // Arrange
        var requester = NewUser("Clara Solicitante");

        // The lookup keys by the loaded user's own id, so the requester must be that user.
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(requester, _briefing.RequesterId);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(_briefing.RequesterId)),
                default))
            .ReturnsAsync([requester]);

        // Act
        var result = await _handler.Handle(new GetBriefingTrackingQuery(_briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Campanha de verão");
        result.Value.Status.Should().Be(BriefingStatus.Draft);
        result.Value.RequesterId.Should().Be(_briefing.RequesterId);
        result.Value.RequesterName.Should().Be("Clara Solicitante");
    }
}
