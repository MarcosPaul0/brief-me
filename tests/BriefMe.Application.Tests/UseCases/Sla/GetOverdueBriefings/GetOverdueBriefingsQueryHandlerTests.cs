using BriefMe.Application.UseCases.Sla.GetOverdueBriefings;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Sla.GetOverdueBriefings;

public class GetOverdueBriefingsQueryHandlerTests
{
    private static readonly DateTime DueAt = new(2026, 7, 20, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime DetectedAt = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly GetOverdueBriefingsQueryHandler _handler;

    public GetOverdueBriefingsQueryHandlerTests()
    {
        _handler = new GetOverdueBriefingsQueryHandler(
            _briefingRepositoryMock.Object,
            _userRepositoryMock.Object,
            _categoryRepositoryMock.Object);

        // Default: no lookup resolves. The tests that care about names override it.
        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);

        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([]);
    }

    /// <summary>
    /// A briefing already marked by the deadline monitor (BRF-043) — the only kind this query ever reads.
    /// </summary>
    private static Briefing NewOverdueBriefing(
        Guid? requesterId = null,
        Guid? categoryId = null,
        DateOnly? desiredDeliveryDate = null,
        string title = "Campanha de verão")
    {
        var briefing = Briefing.Create(
            title,
            Priority.High,
            categoryId ?? Guid.NewGuid(),
            requesterId ?? Guid.NewGuid(),
            "Peças para o lançamento",
            "Aumentar o alcance da campanha",
            "Marketing",
            desiredDeliveryDate);

        // The monitor only ever marks a submitted briefing, and a draft with no typed date cannot reach
        // AwaitingApproval through SubmitForApproval — so the status is written straight in, as in the
        // repository tests of the same slice.
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, BriefingStatus.AwaitingApproval);

        briefing.MarkOverdue(DueAt, DetectedAt);

        return briefing;
    }

    private static User NewUser(Guid id, string fullName)
    {
        var user = User.Create(fullName, Email.Create("solicitante@briefme.test").Value, "hash");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(user, id);

        return user;
    }

    [Fact]
    public async Task Handle_WhenBriefingsAreOverdue_ReturnsThemWithDeliveryDateRequesterAndPaginationMetadata()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var desiredDeliveryDate = new DateOnly(2026, 7, 20);
        var briefing = NewOverdueBriefing(requesterId, categoryId, desiredDeliveryDate);

        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(2, 10, default))
            .ReturnsAsync([briefing]);

        _briefingRepositoryMock
            .Setup(r => r.CountOverdueAsync(default))
            .ReturnsAsync(12);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([NewUser(requesterId, "Ana Souza")]);

        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([new CategoryInfo(categoryId, "Social Media")]);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(2, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(12);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(briefing.Id);
        item.Title.Should().Be("Campanha de verão");
        item.DesiredDeliveryDate.Should().Be(desiredDeliveryDate);
        item.RequesterId.Should().Be(requesterId);
        item.RequesterName.Should().Be("Ana Souza");
        item.CategoryId.Should().Be(categoryId);
        item.CategoryName.Should().Be("Social Media");
        item.OverdueSince.Should().Be(DetectedAt);
        item.Priority.Should().Be(Priority.High);
        item.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task Handle_WhenTwoBriefingsAreOverdue_ReturnsBothInTheRepositoryOrder()
    {
        // Arrange — the BRF-045 acceptance criterion, on a page. Ordering is the repository's (oldest mark
        // first) and the handler must not re-sort it.
        var first = NewOverdueBriefing(title: "Campanha de verão");
        var second = NewOverdueBriefing(title: "Catálogo institucional");

        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([first, second]);

        _briefingRepositoryMock
            .Setup(r => r.CountOverdueAsync(default))
            .ReturnsAsync(2);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(1, 20), default);

        // Assert
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(item => item.Id).Should().ContainInOrder(first.Id, second.Id);
    }

    [Fact]
    public async Task Handle_WhenNothingIsOverdue_ReturnsEmptyPage()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([]);

        _briefingRepositoryMock
            .Setup(r => r.CountOverdueAsync(default))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(1, 20), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenBriefingsShareTheRequester_ResolvesNamesInOneDistinctLookup()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        IReadOnlyCollection<Guid>? requestedIds = null;

        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync(
            [
                NewOverdueBriefing(requesterId),
                NewOverdueBriefing(requesterId, title: "Catálogo institucional")
            ]);

        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => requestedIds = ids)
            .ReturnsAsync([NewUser(requesterId, "Ana Souza")]);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(1, 20), default);

        // Assert
        requestedIds.Should().ContainSingle().Which.Should().Be(requesterId);
        result.Value.Items.Should().OnlyContain(item => item.RequesterName == "Ana Souza");
    }

    [Fact]
    public async Task Handle_WhenBriefingsShareTheCategory_ReadsTheCatalogOnce()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync(
            [
                NewOverdueBriefing(categoryId: categoryId),
                NewOverdueBriefing(categoryId: categoryId, title: "Catálogo institucional")
            ]);

        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync(default))
            .ReturnsAsync([new CategoryInfo(categoryId, "Social Media")]);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(1, 20), default);

        // Assert
        result.Value.Items.Should().OnlyContain(item => item.CategoryName == "Social Media");
        _categoryRepositoryMock.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNamesDoNotResolve_LeavesThemNullAndKeepsTheRow()
    {
        // Arrange — a removed requester or a deactivated category degrades one cell, never the list.
        var briefing = NewOverdueBriefing();

        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([briefing]);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(1, 20), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(briefing.Id);
        item.RequesterName.Should().BeNull();
        item.CategoryName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenBriefingHasNoDesiredDeliveryDate_ReturnsItWithNullDateAndTheMark()
    {
        // Arrange — a briefing with no date of its own can still breach the SLA policy (RF027); the mark,
        // not the date, is what puts it on this list.
        _briefingRepositoryMock
            .Setup(r => r.GetOverdueAsync(1, 20, default))
            .ReturnsAsync([NewOverdueBriefing()]);

        // Act
        var result = await _handler.Handle(new GetOverdueBriefingsQuery(1, 20), default);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.DesiredDeliveryDate.Should().BeNull();
        item.OverdueSince.Should().Be(DetectedAt);
    }
}
