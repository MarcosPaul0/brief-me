using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.RejectBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.RejectBriefing;

public class RejectBriefingCommandHandlerTests
{
    private const string Reason = "Budget not approved for this campaign.";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly RejectBriefingCommandHandler _handler;

    public RejectBriefingCommandHandlerTests()
    {
        _handler = new RejectBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_managerId.ToString());
    }

    /// <summary>Briefing in AwaitingApproval, reached through the real transitions (no reflection).</summary>
    private Briefing BriefingUnderReview()
    {
        var briefing = Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        briefing.AddAttachment("brief.pdf", "/storage/brief.pdf", 1024, _requesterId);
        briefing.SubmitForApproval();
        briefing.ClearDomainEvents();

        return briefing;
    }

    private static void ForceStatus(Briefing briefing, BriefingStatus status) =>
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, status);

    [Fact]
    public async Task Handle_WhenBriefingIsAwaitingApproval_ChangesStatusToRejected()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Rejected);
    }

    [Fact]
    public async Task Handle_WhenRejectionSucceeds_RecordsTheJustification()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        briefing.DecisionJustification!.Value.Should().Be(Reason);
    }

    [Fact]
    public async Task Handle_WhenJustificationHasSurroundingWhitespace_RecordsItTrimmed()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, $"  {Reason}  "), default);

        // Assert
        briefing.DecisionJustification!.Value.Should().Be(Reason);
    }

    [Fact]
    public async Task Handle_WhenRejectionSucceeds_RecordsAuthenticatedUserAsReviewer()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        briefing.ReviewedById.Should().Be(_managerId);
    }

    [Fact]
    public async Task Handle_WhenRejectionSucceeds_RaisesBriefingRejectedEvent()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        briefing.DomainEvents.Should().ContainSingle(e =>
            e is BriefingRejected
            && ((BriefingRejected)e).BriefingId == briefing.Id
            && ((BriefingRejected)e).RequesterId == _requesterId
            && ((BriefingRejected)e).RejectedById == _managerId
            && ((BriefingRejected)e).Justification == Reason);
    }

    [Fact]
    public async Task Handle_WhenRejectionSucceeds_UpdatesAndCommitsOnce()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.Update(briefing), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRejecterIsNotTheRequester_IsStillAllowed()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenJustificationIsNullOrWhitespace_ReturnsFailure(string? justification)
    {
        // Act
        var result = await _handler.Handle(new RejectBriefingCommand(Guid.NewGuid(), justification!), default);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenJustificationIsMissing_DoesNotLoadTheRepository()
    {
        // Act
        await _handler.Handle(new RejectBriefingCommand(Guid.NewGuid(), string.Empty), default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenJustificationIsMissing_KeepsStatusAndDoesNotCommit()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, string.Empty), default);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new RejectBriefingCommand(Guid.NewGuid(), Reason), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_DoesNotCommit()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Briefing?)null);

        // Act
        await _handler.Handle(new RejectBriefingCommand(Guid.NewGuid(), Reason), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingIsNotAwaitingApproval_ReturnsConflict(BriefingStatus status)
    {
        // Arrange
        var briefing = BriefingUnderReview();
        ForceStatus(briefing, status);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsNotAwaitingApproval_KeepsStatusAndDoesNotCommit()
    {
        // Arrange
        var briefing = BriefingUnderReview();
        ForceStatus(briefing, BriefingStatus.Approved);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new RejectBriefingCommand(briefing.Id, Reason), default);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Approved);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized(string? userId)
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns(userId);

        // Act
        var result = await _handler.Handle(new RejectBriefingCommand(Guid.NewGuid(), Reason), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_DoesNotLoadTheRepository()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        await _handler.Handle(new RejectBriefingCommand(Guid.NewGuid(), Reason), default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
