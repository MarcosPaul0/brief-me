using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingsByRequester;

public class GetBriefingsByRequesterQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly GetBriefingsByRequesterQueryHandler _handler;

    public GetBriefingsByRequesterQueryHandlerTests()
    {
        _handler = new GetBriefingsByRequesterQueryHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    private Briefing NewDraft(Guid? categoryId = null) =>
        Briefing.Create("Launch campaign", Priority.Medium, categoryId ?? Guid.NewGuid(), _requesterId);

    [Fact]
    public async Task Handle_WhenRepositoryReturnsPage_ReturnsMappedDtosWithPaginationMetadata()
    {
        // Arrange
        var briefing = NewDraft();
        var query = new GetBriefingsByRequesterQuery(Page: 2, PageSize: 10);

        _briefingRepositoryMock
            .Setup(r => r.GetByRequesterAsync(_requesterId, It.IsAny<BriefingFilter>(), 2, 10, default))
            .ReturnsAsync([briefing]);

        _briefingRepositoryMock
            .Setup(r => r.CountByRequesterAsync(_requesterId, It.IsAny<BriefingFilter>(), default))
            .ReturnsAsync(25);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(briefing.Id);
        item.RequesterId.Should().Be(_requesterId);
        item.Status.Should().Be(BriefingStatus.Draft);
    }

    [Fact]
    public async Task Handle_Always_ScopesQueryToAuthenticatedRequester()
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: 10);

        _briefingRepositoryMock
            .Setup(r => r.GetByRequesterAsync(It.IsAny<Guid>(), It.IsAny<BriefingFilter>(), 1, 10, default))
            .ReturnsAsync([]);

        // Act
        await _handler.Handle(query, default);

        // Assert
        _briefingRepositoryMock.Verify(
            r => r.GetByRequesterAsync(_requesterId, It.IsAny<BriefingFilter>(), 1, 10, default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStatusAndCategoryFiltersInformed_PassesThemToRepository()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var query = new GetBriefingsByRequesterQuery(
            Page: 1,
            PageSize: 20,
            Status: BriefingStatus.Submitted,
            CategoryId: categoryId);

        var expectedFilter = new BriefingFilter(BriefingStatus.Submitted, categoryId);

        _briefingRepositoryMock
            .Setup(r => r.GetByRequesterAsync(_requesterId, expectedFilter, 1, 20, default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _briefingRepositoryMock.Verify(
            r => r.GetByRequesterAsync(_requesterId, expectedFilter, 1, 20, default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoFilters_UsesEmptyFilter()
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: 10);
        var expectedFilter = new BriefingFilter(null, null);

        _briefingRepositoryMock
            .Setup(r => r.GetByRequesterAsync(_requesterId, expectedFilter, 1, 10, default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        _briefingRepositoryMock.Verify(
            r => r.GetByRequesterAsync(_requesterId, expectedFilter, 1, 10, default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutQuerying()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(
            r => r.GetByRequesterAsync(It.IsAny<Guid>(), It.IsAny<BriefingFilter>(), It.IsAny<int>(), It.IsAny<int>(), default),
            Times.Never);
    }
}
