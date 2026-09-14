using BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingsPendingApproval;

public class GetBriefingsPendingApprovalQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly GetBriefingsPendingApprovalQueryHandler _handler;

    public GetBriefingsPendingApprovalQueryHandlerTests()
    {
        _handler = new GetBriefingsPendingApprovalQueryHandler(
            _briefingRepositoryMock.Object,
            _userRepositoryMock.Object,
            _categoryRepositoryMock.Object);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);

        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([]);
    }

    // Same shortcut the sibling handler tests use: the status is forced instead of driven through
    // SubmitForApproval(), which would also require an attachment (BRF-003) irrelevant to this read path.
    private static Briefing NewSubmitted(Guid requesterId, Guid categoryId)
    {
        var briefing = Briefing.Create(
            "Campanha de lançamento",
            Priority.High,
            categoryId,
            requesterId,
            description: "Descrição",
            objective: "Objetivo",
            requestingArea: "Marketing",
            desiredDeliveryDate: new DateOnly(2026, 8, 20));

        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, BriefingStatus.AwaitingApproval);

        return briefing;
    }

    private static User NewUser(Guid id, string fullName)
    {
        var user = User.Create(fullName, Email.Create("solicitante@briefme.test").Value, "hash");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(user, id);

        return user;
    }

    [Fact]
    public async Task Handle_WhenQueueHasBriefings_ReturnsMappedDtosWithPaginationMetadata()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var briefing = NewSubmitted(requesterId, categoryId);

        _briefingRepositoryMock
            .Setup(r => r.GetPendingApprovalAsync(2, 10, default))
            .ReturnsAsync([briefing]);

        _briefingRepositoryMock
            .Setup(r => r.CountPendingApprovalAsync(default))
            .ReturnsAsync(25);

        // Act
        var result = await _handler.Handle(new GetBriefingsPendingApprovalQuery(2, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(briefing.Id);
        item.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task Handle_WhenRequesterExists_FillsRequesterName()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var briefing = NewSubmitted(requesterId, Guid.NewGuid());
        var requester = NewUser(requesterId, "Ana Souza");

        _briefingRepositoryMock
            .Setup(r => r.GetPendingApprovalAsync(1, 20, default))
            .ReturnsAsync([briefing]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([requester]);

        // Act
        var result = await _handler.Handle(new GetBriefingsPendingApprovalQuery(1, 20), default);

        // Assert
        result.Value.Items.Single().RequesterName.Should().Be("Ana Souza");
    }

    [Fact]
    public async Task Handle_WhenCategoryIsInCatalog_FillsCategoryName()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var briefing = NewSubmitted(Guid.NewGuid(), categoryId);

        _briefingRepositoryMock
            .Setup(r => r.GetPendingApprovalAsync(1, 20, default))
            .ReturnsAsync([briefing]);

        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([new CategoryInfo(categoryId, "Campanha de Marketing")]);

        // Act
        var result = await _handler.Handle(new GetBriefingsPendingApprovalQuery(1, 20), default);

        // Assert
        result.Value.Items.Single().CategoryName.Should().Be("Campanha de Marketing");
    }

    [Fact]
    public async Task Handle_WhenRequesterAndCategoryAreUnknown_LeavesNamesNull()
    {
        // Arrange
        var briefing = NewSubmitted(Guid.NewGuid(), Guid.NewGuid());

        _briefingRepositoryMock
            .Setup(r => r.GetPendingApprovalAsync(1, 20, default))
            .ReturnsAsync([briefing]);

        // Act
        var result = await _handler.Handle(new GetBriefingsPendingApprovalQuery(1, 20), default);

        // Assert
        var item = result.Value.Items.Single();
        item.RequesterName.Should().BeNull();
        item.CategoryName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSeveralBriefingsShareRequesters_ResolvesNamesInASingleLookup()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var briefings = new List<Briefing>
        {
            NewSubmitted(requesterId, Guid.NewGuid()),
            NewSubmitted(requesterId, Guid.NewGuid())
        };

        _briefingRepositoryMock
            .Setup(r => r.GetPendingApprovalAsync(1, 20, default))
            .ReturnsAsync(briefings);

        // Act
        await _handler.Handle(new GetBriefingsPendingApprovalQuery(1, 20), default);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenQueueIsEmpty_ReturnsEmptyPage()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetPendingApprovalAsync(1, 20, default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new GetBriefingsPendingApprovalQuery(1, 20), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}
