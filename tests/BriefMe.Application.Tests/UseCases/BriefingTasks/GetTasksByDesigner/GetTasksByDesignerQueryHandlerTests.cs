using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.BriefingTasks.GetTasksByDesigner;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.GetTasksByDesigner;

public class GetTasksByDesignerQueryHandlerTests
{
    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly GetTasksByDesignerQueryHandler _handler;

    public GetTasksByDesignerQueryHandlerTests()
    {
        _handler = new GetTasksByDesignerQueryHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _currentUserMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_designerId.ToString());

        // Default: nothing resolves. The tests that care about titles override it.
        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
    }

    private static Briefing NewBriefing(string title) =>
        Briefing.Create(title, Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

    private BriefingTask NewTask(Guid briefingId, string title = "Banner topo") =>
        BriefingTask.Create(briefingId, title, Priority.High, _designerId);

    [Fact]
    public async Task Handle_WhenDesignerHasTasks_ReturnsPageScopedToCaller()
    {
        // Arrange
        var briefing = NewBriefing("Campanha de verão");
        var task = NewTask(briefing.Id);
        var query = new GetTasksByDesignerQuery(Page: 2, PageSize: 10);

        _taskRepositoryMock
            .Setup(r => r.GetByDesignerAsync(_designerId, It.IsAny<BriefingTaskFilter>(), 2, 10, default))
            .ReturnsAsync([task]);

        _taskRepositoryMock
            .Setup(r => r.CountByDesignerAsync(_designerId, It.IsAny<BriefingTaskFilter>(), default))
            .ReturnsAsync(25);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([briefing]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(task.Id);
        item.Title.Should().Be("Banner topo");
        item.BriefingId.Should().Be(briefing.Id);
        item.BriefingTitle.Should().Be("Campanha de verão");
        item.Status.Should().Be(BriefingTaskStatus.NotStarted);
    }

    [Fact]
    public async Task Handle_WhenTasksShareBriefings_ResolvesTitlesInOneDistinctLookup()
    {
        // Arrange
        var briefing = NewBriefing("Campanha de verão");
        var query = new GetTasksByDesignerQuery(Page: 1, PageSize: 10);
        IReadOnlyCollection<Guid>? requestedIds = null;

        _taskRepositoryMock
            .Setup(r => r.GetByDesignerAsync(_designerId, It.IsAny<BriefingTaskFilter>(), 1, 10, default))
            .ReturnsAsync([NewTask(briefing.Id, "Banner topo"), NewTask(briefing.Id, "Post feed")]);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => requestedIds = ids)
            .ReturnsAsync([briefing]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        requestedIds.Should().ContainSingle().Which.Should().Be(briefing.Id);
        result.Value.Items.Should().OnlyContain(item => item.BriefingTitle == "Campanha de verão");
    }

    [Fact]
    public async Task Handle_WhenBriefingIsNotResolved_LeavesTitleNullAndKeepsTheRow()
    {
        // Arrange
        var query = new GetTasksByDesignerQuery(Page: 1, PageSize: 10);
        var task = NewTask(Guid.NewGuid());

        _taskRepositoryMock
            .Setup(r => r.GetByDesignerAsync(_designerId, It.IsAny<BriefingTaskFilter>(), 1, 10, default))
            .ReturnsAsync([task]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(task.Id);
        item.BriefingTitle.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenFiltersAreSet_PassesTheSameFilterToSliceAndCount()
    {
        // Arrange
        var query = new GetTasksByDesignerQuery(
            Page: 1,
            PageSize: 10,
            Status: BriefingTaskStatus.Blocked,
            Priority: Priority.Urgent);

        var expectedFilter = new BriefingTaskFilter(BriefingTaskStatus.Blocked, Priority.Urgent);

        _taskRepositoryMock
            .Setup(r => r.GetByDesignerAsync(_designerId, expectedFilter, 1, 10, default))
            .ReturnsAsync([]);

        _taskRepositoryMock
            .Setup(r => r.CountByDesignerAsync(_designerId, expectedFilter, default))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert — the strict setups above only match the expected filter; a mismatch would surface as
        // an unconfigured call and fail the mapping below.
        result.IsSuccess.Should().BeTrue();
        _taskRepositoryMock.Verify(r => r.GetByDesignerAsync(_designerId, expectedFilter, 1, 10, default), Times.Once);
        _taskRepositoryMock.Verify(r => r.CountByDesignerAsync(_designerId, expectedFilter, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);
        var query = new GetTasksByDesignerQuery(Page: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_DoesNotReachTheRepositories()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");
        var query = new GetTasksByDesignerQuery(Page: 1, PageSize: 10);

        // Act
        await _handler.Handle(query, default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByDesignerAsync(It.IsAny<Guid>(), It.IsAny<BriefingTaskFilter>(), It.IsAny<int>(), It.IsAny<int>(), default),
            Times.Never);
    }
}
