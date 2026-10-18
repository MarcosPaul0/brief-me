using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.AcceptBriefingDelivery;

public class AcceptBriefingDeliveryCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly AcceptBriefingDeliveryCommandHandler _handler;

    public AcceptBriefingDeliveryCommandHandlerTests()
    {
        _handler = new AcceptBriefingDeliveryCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    private Briefing InProgressBriefing()
    {
        var briefing = Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), _requesterId,
            "description", "objective", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        briefing.Submit();
        briefing.Claim(_designerId);
        _briefingRepositoryMock.Setup(r => r.GetByIdAsync(briefing.Id, default)).ReturnsAsync(briefing);
        return briefing;
    }

    private Briefing DeliveredBriefing()
    {
        var briefing = InProgressBriefing();
        briefing.Deliver(_designerId, "https://files.example/v1");
        return briefing;
    }

    [Fact]
    public async Task Handle_WhenOwnerAcceptsADelivery_CompletesAndCommits()
    {
        // Arrange
        var briefing = DeliveredBriefing();

        // Act
        var result = await _handler.Handle(new AcceptBriefingDeliveryCommand(briefing.Id), default);

        // Assert
        result.Value.Status.Should().Be(BriefingStatus.Completed);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbiddenWithoutCommitting()
    {
        // Arrange
        var briefing = DeliveredBriefing();
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(new AcceptBriefingDeliveryCommand(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotDeliveredYet_ReturnsConflict()
    {
        // Arrange
        var briefing = InProgressBriefing();

        // Act
        var result = await _handler.Handle(new AcceptBriefingDeliveryCommand(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _briefingRepositoryMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new AcceptBriefingDeliveryCommand(id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }
}
