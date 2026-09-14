using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.RemoveBriefingAttachment;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.RemoveBriefingAttachment;

public class RemoveBriefingAttachmentCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly RemoveBriefingAttachmentCommandHandler _handler;

    public RemoveBriefingAttachmentCommandHandlerTests()
    {
        _handler = new RemoveBriefingAttachmentCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
    }

    /// <summary>
    /// A briefing holding <paramref name="attachmentCount"/> reference files, arranged in the status given.
    /// The files are attached while the briefing is still a draft, then the status is forced — otherwise
    /// <c>AddAttachment</c> would refuse the very states this class needs to test removal against.
    /// </summary>
    private Briefing ArrangeBriefing(
        BriefingStatus status = BriefingStatus.Draft,
        Guid? requesterId = null,
        int attachmentCount = 1)
    {
        var briefing = Briefing.Create(
            "Campanha de fim de ano",
            Priority.Medium,
            Guid.NewGuid(),
            requesterId ?? _requesterId);

        for (var index = 0; index < attachmentCount; index++)
            briefing.AddAttachment($"referencia-{index}.pdf", $"2026/07/31/{index}.pdf", 1024, briefing.RequesterId);

        ForceStatus(briefing, status);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private static void ForceStatus(Briefing briefing, BriefingStatus status)
    {
        typeof(Briefing)
            .GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, status);
    }

    [Fact]
    public async Task Handle_WhenAttachmentBelongsToTheBriefing_RemovesItAndReturnsWhatRemains()
    {
        // Arrange
        var briefing = ArrangeBriefing(attachmentCount: 2);
        var target = briefing.Attachments.First();

        // Act
        var result = await _handler.Handle(new RemoveBriefingAttachmentCommand(briefing.Id, target.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Attachments.Should().ContainSingle().Which.Id.Should().NotBe(target.Id);
    }

    [Fact]
    public async Task Handle_WhenRemovalSucceeds_CommitsOnce()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        var target = briefing.Attachments.First();

        // Act
        await _handler.Handle(new RemoveBriefingAttachmentCommand(briefing.Id, target.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAttachmentIsNotOnTheBriefing_ReturnsNotFoundWithoutCommitting()
    {
        // Arrange
        var briefing = ArrangeBriefing();

        // Act
        var result = await _handler.Handle(new RemoveBriefingAttachmentCommand(briefing.Id, Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingErrors.AttachmentNotFound);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingHasMovedOn_ReturnsConflictWithoutCommitting(BriefingStatus status)
    {
        // Arrange
        var briefing = ArrangeBriefing(status);
        var target = briefing.Attachments.First();

        // Act
        var result = await _handler.Handle(new RemoveBriefingAttachmentCommand(briefing.Id, target.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbidden()
    {
        // Arrange
        var briefing = ArrangeBriefing(requesterId: Guid.NewGuid());
        var target = briefing.Attachments.First();

        // Act
        var result = await _handler.Handle(new RemoveBriefingAttachmentCommand(briefing.Id, target.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(
            new RemoveBriefingAttachmentCommand(Guid.NewGuid(), Guid.NewGuid()),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutLoading()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(
            new RemoveBriefingAttachmentCommand(Guid.NewGuid(), Guid.NewGuid()),
            default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(
            r => r.GetByIdWithAttachmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
