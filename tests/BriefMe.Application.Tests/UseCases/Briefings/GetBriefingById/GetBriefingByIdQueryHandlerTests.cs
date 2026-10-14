using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingById;

public class GetBriefingByIdQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly GetBriefingByIdQueryHandler _handler;

    public GetBriefingByIdQueryHandlerTests()
    {
        _handler = new GetBriefingByIdQueryHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    private Briefing NewDraft() =>
        Briefing.Create("Original title", Priority.Low, Guid.NewGuid(), _requesterId);

    [Fact]
    public async Task Handle_WhenOwnedByRequester_ReturnsBriefingDetailDto()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(briefing.Id);
        result.Value.RequesterId.Should().Be(_requesterId);
        result.Value.Status.Should().Be(BriefingStatus.Draft);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenNotTheOwner_ReturnsForbidden()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutLoading()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
