using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.DeliverBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.DeliverBriefing;

public class DeliverBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly DeliverBriefingCommandHandler _handler;

    public DeliverBriefingCommandHandlerTests()
    {
        _handler = new DeliverBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_designerId.ToString());
    }

    private Briefing ClaimedBriefing(Guid? designerId = null)
    {
        var briefing = Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), Guid.NewGuid(),
            "description", "objective", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        briefing.Submit();
        briefing.Claim(designerId ?? _designerId);
        _briefingRepositoryMock.Setup(r => r.GetByIdAsync(briefing.Id, default)).ReturnsAsync(briefing);
        return briefing;
    }

    [Fact]
    public async Task Handle_WhenAssigneeDelivers_StoresNoteAndCommits()
    {
        // Arrange
        var briefing = ClaimedBriefing();

        // Act
        var result = await _handler.Handle(new DeliverBriefingCommand(briefing.Id, "https://files.example/v1"), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Status = BriefingStatus.Delivered, DeliveryNote = "https://files.example/v1" });
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheAssignee_ReturnsForbiddenWithoutCommitting()
    {
        // Arrange
        var briefing = ClaimedBriefing(designerId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(new DeliverBriefingCommand(briefing.Id, "https://files.example/v1"), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_WhenNoteIsBlank_ReturnsFailureWithoutLoading(string? note)
    {
        // Arrange
        // (none)

        // Act
        var result = await _handler.Handle(new DeliverBriefingCommand(Guid.NewGuid(), note), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDelivered_ReturnsConflict()
    {
        // Arrange
        var briefing = ClaimedBriefing();
        briefing.Deliver(_designerId, "https://files.example/v1");

        // Act
        var result = await _handler.Handle(new DeliverBriefingCommand(briefing.Id, "https://files.example/v2"), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
    }
}
