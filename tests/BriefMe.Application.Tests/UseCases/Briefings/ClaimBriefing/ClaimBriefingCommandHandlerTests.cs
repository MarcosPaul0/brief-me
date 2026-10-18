using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.ClaimBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.ClaimBriefing;

public class ClaimBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly ClaimBriefingCommandHandler _handler;

    public ClaimBriefingCommandHandlerTests()
    {
        _handler = new ClaimBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_designerId.ToString());
    }

    private Briefing QueuedBriefing()
    {
        var briefing = Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), Guid.NewGuid(),
            "description", "objective", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        briefing.Submit();
        _briefingRepositoryMock.Setup(r => r.GetByIdAsync(briefing.Id, default)).ReturnsAsync(briefing);
        return briefing;
    }

    [Fact]
    public async Task Handle_WhenBriefingIsQueued_AssignsCallerAndCommits()
    {
        // Arrange
        var briefing = QueuedBriefing();

        // Act
        var result = await _handler.Handle(new ClaimBriefingCommand(briefing.Id), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { DesignerId = (Guid?)_designerId, Status = BriefingStatus.InProgress });
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyClaimed_ReturnsConflictWithoutCommitting()
    {
        // Arrange
        var briefing = QueuedBriefing();
        briefing.Claim(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(new ClaimBriefingCommand(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnotherDesignerCommitsFirst_ReturnsAlreadyClaimedConflict()
    {
        // Arrange
        var briefing = QueuedBriefing();
        _unitOfWorkMock
            .Setup(u => u.CommitAsync(default))
            .ThrowsAsync(new ConcurrencyConflictException("stale"));

        // Act
        var result = await _handler.Handle(new ClaimBriefingCommand(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingErrors.AlreadyClaimed);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _briefingRepositoryMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new ClaimBriefingCommand(id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }
}
