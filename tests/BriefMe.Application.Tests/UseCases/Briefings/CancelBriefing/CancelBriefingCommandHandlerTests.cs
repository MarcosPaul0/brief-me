using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.CancelBriefing;

public class CancelBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly CancelBriefingCommandHandler _handler;

    public CancelBriefingCommandHandlerTests()
    {
        _handler = new CancelBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }
    
    private Briefing NewDraft() =>
        Briefing.Create("Original title", Priority.Low, Guid.NewGuid(), _requesterId);
    
    [Fact]
    public async Task Handle_WhenCommandIsValid_PersistsCanceledAndReturnsDto()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Cancelled);
        result.Value.RequesterId.Should().Be(_requesterId);
    }

    [Fact]
    public async Task Handle_WhenCommandIsValid_UpdateBriefingAndCommitsOnce()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        await _handler.Handle(command, default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.Update(It.IsAny<Briefing>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }
    
    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        var briefing = NewDraft();
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        _briefingRepositoryMock.Verify(r => r.Update(It.IsAny<Briefing>()), Times.Never);
    }

    // InProgress/Completed have no entry transition yet (BRF-015+), so Cancelled is the only
    // non-cancellable state reachable through the public API today.
    [Fact]
    public async Task Handle_WhenBriefingIsAlreadyCancelled_ReturnsConflict()
    {
        // Arrange
        var briefing = NewDraft();
        briefing.Cancel();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsAlreadyCancelled_DoesNotPersist()
    {
        // Arrange
        var briefing = NewDraft();
        briefing.Cancel();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        await _handler.Handle(command, default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.Update(It.IsAny<Briefing>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRequesterIsNotTheOwner_ReturnsForbidden()
    {
        // Arrange
        var briefing = Briefing.Create("Original title", Priority.Low, Guid.NewGuid(), Guid.NewGuid());
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var briefing = NewDraft();
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        var command = new CancelBriefingCommand(briefing.Id);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(r => r.Update(It.IsAny<Briefing>()), Times.Never);
    }
}