using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.RequestBriefingChanges;

public class RequestBriefingChangesCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly RequestBriefingChangesCommandHandler _handler;

    public RequestBriefingChangesCommandHandlerTests()
    {
        _handler = new RequestBriefingChangesCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    private Briefing DeliveredBriefing(bool delivered = true)
    {
        var briefing = Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), _requesterId,
            "description", "objective", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        briefing.Submit();
        briefing.Claim(_designerId);
        if (delivered)
            briefing.Deliver(_designerId, "https://files.example/v1");
        _briefingRepositoryMock.Setup(r => r.GetByIdAsync(briefing.Id, default)).ReturnsAsync(briefing);
        return briefing;
    }

    [Fact]
    public async Task Handle_WhenOwnerSendsBackADelivery_ReturnsToInProgressWithReason()
    {
        // Arrange
        var briefing = DeliveredBriefing();

        // Act
        var result = await _handler.Handle(new RequestBriefingChangesCommand(briefing.Id, "Use the new logo"), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Status = BriefingStatus.InProgress, ChangeRequestReason = "Use the new logo", DesignerId = (Guid?)_designerId });
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_WhenReasonIsBlank_ReturnsFailureWithoutLoading(string? reason)
    {
        // Arrange
        // (none)

        // Act
        var result = await _handler.Handle(new RequestBriefingChangesCommand(Guid.NewGuid(), reason), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbidden()
    {
        // Arrange
        var briefing = DeliveredBriefing();
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(new RequestBriefingChangesCommand(briefing.Id, "Use the new logo"), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenNotDelivered_ReturnsConflictWithoutCommitting()
    {
        // Arrange
        var briefing = DeliveredBriefing(delivered: false);

        // Act
        var result = await _handler.Handle(new RequestBriefingChangesCommand(briefing.Id, "Use the new logo"), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
