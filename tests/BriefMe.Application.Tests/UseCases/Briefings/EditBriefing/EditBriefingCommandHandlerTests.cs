using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.EditBriefing;

public class EditBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly EditBriefingCommandHandler _handler;

    public EditBriefingCommandHandlerTests()
    {
        _handler = new EditBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object,
            new BriefingAttachmentStager(_fileStorageMock.Object),
            _fileStorageMock.Object);

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
            .Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default))
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
            .Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default))
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
            .Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default))
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
            .Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default))
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
            .Setup(r => r.GetByIdWithAttachmentsAsync(It.IsAny<Guid>(), default))
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
            .Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default))
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
        _briefingRepositoryMock.Verify(r => r.GetByIdWithAttachmentsAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    private Briefing DraftWithAttachments(int count)
    {
        var briefing = NewDraft();
        foreach (var index in Enumerable.Range(0, count))
            briefing.AddAttachment($"file-{index}.pdf", $"briefings/old-{index}.pdf", AttachmentFormat.Pdf, 100);

        _briefingRepositoryMock.Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default)).ReturnsAsync(briefing);
        return briefing;
    }

    private EditBriefingCommand EditAttachments(Guid briefingId, IReadOnlyList<AttachmentUpload>? added = null, IReadOnlyList<Guid>? removed = null) =>
        EditCommand(briefingId) with { NewAttachments = added, RemovedAttachmentIds = removed };

    [Fact]
    public async Task Handle_WhenAddingAndRemovingAttachments_UpdatesTheCollection()
    {
        // Arrange
        var briefing = DraftWithAttachments(2);
        var kept = briefing.Attachments.First();
        var dropped = briefing.Attachments.Last();

        // Act
        var result = await _handler.Handle(EditAttachments(briefing.Id, [TestUploads.Mp4("new.mp4")], [dropped.Id]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Attachments.Select(a => a.FileName).Should().Equal(kept.FileName, "new.mp4");
    }

    [Fact]
    public async Task Handle_WhenRemovingAttachment_DeletesItsFileOnlyAfterCommit()
    {
        // Arrange
        var briefing = DraftWithAttachments(1);
        var dropped = briefing.Attachments.Single();
        var calls = new List<string>();
        _unitOfWorkMock.Setup(u => u.CommitAsync(default)).Callback(() => calls.Add("commit")).Returns(Task.CompletedTask);
        _fileStorageMock
            .Setup(s => s.DeleteAsync(dropped.StorageKey, default))
            .Callback(() => calls.Add("delete"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(EditAttachments(briefing.Id, removed: [dropped.Id]), default);

        // Assert
        calls.Should().Equal("commit", "delete");
    }

    [Fact]
    public async Task Handle_WhenRemovedIdIsNotAnAttachmentOfTheBriefing_ReturnsNotFoundWithoutChanges()
    {
        // Arrange
        var briefing = DraftWithAttachments(1);

        // Act
        var result = await _handler.Handle(EditAttachments(briefing.Id, [TestUploads.Pdf()], [Guid.NewGuid()]), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingErrors.AttachmentNotFound);
        _fileStorageMock.VerifyNoOtherCalls();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTotalAfterEditExceedsMaximum_FailsWithoutStoring()
    {
        // Arrange
        var briefing = DraftWithAttachments(8);

        // Act
        var result = await _handler.Handle(
            EditAttachments(briefing.Id, [TestUploads.Pdf(), TestUploads.Pdf(), TestUploads.Pdf()]), default);

        // Assert
        result.Error.Should().Be(BriefingErrors.TooManyAttachments(Briefing.MaxAttachments));
        _fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenRemovalsMakeRoomForNewAttachments_Succeeds()
    {
        // Arrange
        var briefing = DraftWithAttachments(8);
        var removed = briefing.Attachments.Take(2).Select(a => a.Id).ToList();
        var added = new[] { TestUploads.Pdf(), TestUploads.Pdf(), TestUploads.Pdf(), TestUploads.Pdf() };

        // Act
        var result = await _handler.Handle(EditAttachments(briefing.Id, added, removed), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Attachments.Should().HaveCount(Briefing.MaxAttachments);
    }

    [Fact]
    public async Task Handle_WhenNotOwner_DoesNotTouchStorage()
    {
        // Arrange
        var briefing = Briefing.Create("Someone else's", Priority.Low, Guid.NewGuid(), Guid.NewGuid());
        _briefingRepositoryMock.Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default)).ReturnsAsync(briefing);

        // Act
        var result = await _handler.Handle(EditAttachments(briefing.Id, [TestUploads.Pdf()]), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        _fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenBriefingIsClaimed_DoesNotTouchStorage()
    {
        // Arrange
        var briefing = DraftWithAttachments(1);
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        var result = await _handler.Handle(
            EditAttachments(briefing.Id, [TestUploads.Pdf()], [briefing.Attachments.Single().Id]), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenNewAttachmentHasUnsupportedFormat_FailsWithoutCommitting()
    {
        // Arrange
        var briefing = DraftWithAttachments(0);

        // Act
        var result = await _handler.Handle(EditAttachments(briefing.Id, [TestUploads.Html("evil.pdf")]), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCommitFails_DiscardsNewFilesAndKeepsRemovedOnes()
    {
        // Arrange
        var briefing = DraftWithAttachments(1);
        var dropped = briefing.Attachments.Single();
        string? newKey = null;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
            .Callback<string, Stream, string, CancellationToken>((key, _, _, _) => newKey = key);
        _unitOfWorkMock.Setup(u => u.CommitAsync(default)).ThrowsAsync(new InvalidOperationException("db down"));

        // Act
        var act = () => _handler.Handle(EditAttachments(briefing.Id, [TestUploads.Png()], [dropped.Id]), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _fileStorageMock.Verify(s => s.DeleteAsync(newKey!, It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteAsync(dropped.StorageKey, It.IsAny<CancellationToken>()), Times.Never);
    }
}
