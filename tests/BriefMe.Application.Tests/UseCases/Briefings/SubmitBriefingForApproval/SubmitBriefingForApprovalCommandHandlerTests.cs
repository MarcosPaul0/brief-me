using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.SubmitBriefingForApproval;

public class SubmitBriefingForApprovalCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly SubmitBriefingForApprovalCommandHandler _handler;

    public SubmitBriefingForApprovalCommandHandlerTests()
    {
        _handler = new SubmitBriefingForApprovalCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    private Briefing CompleteDraft() =>
        Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

    private static void ForceStatus(Briefing briefing, BriefingStatus status) =>
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, status);

    [Fact]
    public async Task Handle_WhenDraftHasAllMandatoryFields_TransitionsToAwaitingApproval()
    {
        // Arrange
        var briefing = CompleteDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task Handle_WhenSubmissionSucceeds_UpdatesAndCommitsOnce()
    {
        // Arrange
        var briefing = CompleteDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(new SubmitBriefingForApprovalCommand(briefing.Id), default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.Update(briefing), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMandatoryFieldsMissing_ReturnsValidationFailureWithoutCommitting()
    {
        // Arrange
        var briefing = Briefing.Create("Bare draft", Priority.Low, Guid.NewGuid(), _requesterId);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().NotBeEmpty();
        briefing.Status.Should().Be(BriefingStatus.Draft);
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
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotTheOwner_ReturnsForbiddenWithoutCommitting()
    {
        // Arrange
        var briefing = CompleteDraft();
        ForceStatus(briefing, BriefingStatus.Draft);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingAlreadyAwaitingApproval_ReturnsConflict()
    {
        // Arrange
        var briefing = CompleteDraft();
        ForceStatus(briefing, BriefingStatus.AwaitingApproval);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenChangesRequested_IsSubmittable()
    {
        // Arrange
        var briefing = CompleteDraft();
        ForceStatus(briefing, BriefingStatus.ChangesRequested);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutLoading()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new SubmitBriefingForApprovalCommand(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
