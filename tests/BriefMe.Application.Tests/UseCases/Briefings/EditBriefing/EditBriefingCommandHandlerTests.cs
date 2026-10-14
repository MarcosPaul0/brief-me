using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.EditBriefing;

public class EditBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly EditBriefingCommandHandler _handler;

    public EditBriefingCommandHandlerTests()
    {
        _handler = new EditBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    private Briefing NewDraft() =>
        Briefing.Create("Original title", Priority.Low, Guid.NewGuid(), _requesterId);

    private static void ForceStatus(Briefing briefing, BriefingStatus status) =>
        typeof(Briefing).GetProperty(nameof(Briefing.Status))!.SetValue(briefing, status);

    private EditBriefingCommand EditCommand(Guid briefingId) => new(
        briefingId,
        "Edited title",
        Priority.Urgent,
        Description: "New description",
        Objective: "New objective",
        RequestingArea: "Design");

    [Fact]
    public async Task Handle_WhenDraftOwnedByRequester_UpdatesContentAndReturnsDto()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(EditCommand(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Edited title");
        result.Value.Priority.Should().Be(Priority.Urgent);
        result.Value.Objective.Should().Be("New objective");
    }

    [Fact]
    public async Task Handle_WhenEditSucceeds_UpdatesAndCommitsOnce()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        await _handler.Handle(EditCommand(briefing.Id), default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.Update(briefing), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStatusIsSubmitted_IsEditable()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.Submitted);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(EditCommand(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Edited title");
    }

    [Fact]
    public async Task Handle_WhenStatusIsInProgress_ReturnsEditNotAllowedConflictWithoutCommitting()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(EditCommand(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
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
        var result = await _handler.Handle(EditCommand(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotTheOwner_ReturnsForbiddenWithoutCommitting()
    {
        // Arrange
        var briefing = NewDraft();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, default))
            .ReturnsAsync(briefing);
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(EditCommand(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutLoading()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(EditCommand(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
