using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.GetTaskDetail;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.Tests.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.GetTaskDetail;

public class GetTaskDetailQueryHandlerTests
{
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly GetTaskDetailQueryHandler _handler;

    public GetTaskDetailQueryHandlerTests()
    {
        _handler = new GetTaskDetailQueryHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _currentUserMock.Object);

        // Default caller: the assignee, the common path — the tests about the other involved parties
        // override it.
        _currentUserMock.Setup(u => u.UserId).Returns(_designerId.ToString());

        // Default: nothing extra resolves. The tests that care about names or deliveries override these.
        _briefingRepositoryMock
            .Setup(r => r.GetDeliveriesByTaskAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
    }

    private static Briefing NewBriefing(string title = "Campanha de verão") =>
        Briefing.Create(title, Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

    private BriefingTask NewTask(Guid briefingId, Guid? designerId = null) =>
        BriefingTask.Create(briefingId, "Banner topo", Priority.High, designerId ?? _designerId);

    private static User NewUser(string fullName) =>
        User.Create(fullName, Email.Create($"{Guid.NewGuid():N}@briefme.test").Value, "hash");

    private void GivenTask(BriefingTask task) =>
        _taskRepositoryMock
            .Setup(r => r.GetByIdWithCommentsAsync(task.Id, default))
            .ReturnsAsync(task);

    private void GivenBriefing(Briefing briefing) =>
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

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
        var result = await _handler.Handle(new GetTaskDetailQuery(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _taskRepositoryMock.Verify(
            r => r.GetByIdWithCommentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithCommentsAsync(taskId, default))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(taskId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotInvolved_ReturnsForbidden()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id, designerId: Guid.NewGuid());
        var outsider = NewUser("Estranho");

        _currentUserMock.Setup(u => u.UserId).Returns(outsider.Id.ToString());
        GivenTask(task);
        GivenBriefing(briefing);

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(outsider.Id, default))
            .ReturnsAsync(outsider);

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotInvolvedToView);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheAssignee_ReturnsDetailWithBriefingTitle()
    {
        // Arrange
        var briefing = NewBriefing("Campanha de verão");
        var task = NewTask(briefing.Id);
        GivenTask(task);
        GivenBriefing(briefing);
        GivenPeople(NewUser("Ana Designer"));

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(task.Id);
        result.Value.Title.Should().Be("Banner topo");
        result.Value.BriefingId.Should().Be(briefing.Id);
        result.Value.BriefingTitle.Should().Be("Campanha de verão");
        result.Value.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheRequester_ReturnsDetail()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id, designerId: Guid.NewGuid());

        _currentUserMock.Setup(u => u.UserId).Returns(briefing.RequesterId.ToString());
        GivenTask(task);
        GivenBriefing(briefing);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.Verify(
            r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerHasManagementRole_ReturnsDetail()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id, designerId: Guid.NewGuid());
        var manager = NewUser("Marco Gestor");
        var managerRole = Role.Create(DomainRoles.GestorDeDesign, "Gestão", isSystemRole: true);
        manager.AssignRole(managerRole.Id);

        _currentUserMock.Setup(u => u.UserId).Returns(manager.Id.ToString());
        GivenTask(task);
        GivenBriefing(briefing);

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(manager.Id, default))
            .ReturnsAsync(manager);

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(default))
            .ReturnsAsync([managerRole]);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTaskHasComments_ResolvesAuthorNames()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id);
        var author = NewUser("Ana Designer");
        task.AddComment(author.Id, "Primeira versão enviada.");

        GivenTask(task);
        GivenBriefing(briefing);
        GivenPeople(author);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert
        var comment = result.Value.Comments.Should().ContainSingle().Subject;
        comment.AuthorId.Should().Be(author.Id);
        comment.AuthorName.Should().Be("Ana Designer");
        comment.Text.Should().Be("Primeira versão enviada.");
    }

    [Fact]
    public async Task Handle_WhenTaskHasDeliveries_ResolvesUploaderNames()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id);
        var uploader = NewUser("Ana Designer");
        var delivery = Attachment.CreateDelivery(briefing.Id, task.Id, "banner.png", "key", 2048, uploader.Id);

        GivenTask(task);
        GivenBriefing(briefing);
        GivenPeople(uploader);

        _briefingRepositoryMock
            .Setup(r => r.GetDeliveriesByTaskAsync(task.Id, default))
            .ReturnsAsync([delivery]);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert — RF052: a delivery shows who produced it and when.
        var item = result.Value.Deliveries.Should().ContainSingle().Subject;
        item.FileName.Should().Be("banner.png");
        item.SizeBytes.Should().Be(2048);
        item.UploadedById.Should().Be(uploader.Id);
        item.UploadedByName.Should().Be("Ana Designer");
        item.UploadedAt.Should().Be(delivery.UploadedAt);
    }

    [Fact]
    public async Task Handle_WhenAuthorIsMissing_LeavesNameNull()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id);
        task.AddComment(Guid.NewGuid(), "Comentário de quem já saiu.");

        GivenTask(task);
        GivenBriefing(briefing);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Comments.Should().ContainSingle().Which.AuthorName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPeopleRepeat_ResolvesThemInOneDistinctLookup()
    {
        // Arrange
        var briefing = NewBriefing();
        var task = NewTask(briefing.Id);
        var author = NewUser("Ana Designer");
        task.AddComment(author.Id, "Primeira versão.");
        task.AddComment(author.Id, "Segunda versão.");
        IReadOnlyCollection<Guid>? requestedIds = null;

        GivenTask(task);
        GivenBriefing(briefing);

        _briefingRepositoryMock
            .Setup(r => r.GetDeliveriesByTaskAsync(task.Id, default))
            .ReturnsAsync([Attachment.CreateDelivery(briefing.Id, task.Id, "banner.png", "key", 10, author.Id)]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => requestedIds = ids)
            .ReturnsAsync([author]);

        // Act
        await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert — the author commented twice, delivered once and is the assignee: still one id, one query.
        _userRepositoryMock.Verify(
            r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default),
            Times.Once);
        requestedIds.Should().NotBeNull();
        requestedIds.Should().BeEquivalentTo([author.Id, _designerId]);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsMissing_ReturnsDetailWithoutTitle()
    {
        // Arrange
        var task = NewTask(Guid.NewGuid());
        GivenTask(task);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(task.BriefingId, default))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new GetTaskDetailQuery(task.Id), default);

        // Assert — the assignee still reads the record; only the header's briefing line degrades.
        result.IsSuccess.Should().BeTrue();
        result.Value.BriefingTitle.Should().BeNull();
    }
}
