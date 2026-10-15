using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.GetBriefingAttachmentContent;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingAttachmentContent;

public class GetBriefingAttachmentContentQueryHandlerTests
{
    private const string Token = "valid-token";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Mock<IAttachmentLinkSigner> _signerMock = new();
    private readonly GetBriefingAttachmentContentQueryHandler _handler;

    public GetBriefingAttachmentContentQueryHandlerTests()
    {
        _handler = new GetBriefingAttachmentContentQueryHandler(
            _briefingRepositoryMock.Object,
            _fileStorageMock.Object,
            _signerMock.Object);
    }

    private (Briefing Briefing, BriefingAttachment Attachment) BriefingWithVideo()
    {
        var briefing = Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());
        var attachment = briefing.AddAttachment("clip.mp4", "briefings/clip.mp4", AttachmentFormat.Mp4, 100);
        _briefingRepositoryMock.Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default)).ReturnsAsync(briefing);
        _signerMock.Setup(s => s.Verify(briefing.Id, attachment.Id, Token)).Returns(true);
        return (briefing, attachment);
    }

    [Fact]
    public async Task Handle_WhenTokenIsValid_ReturnsStreamWithDetectedTypeAndName()
    {
        // Arrange
        var (briefing, attachment) = BriefingWithVideo();
        var content = new MemoryStream([1, 2, 3]);
        _fileStorageMock.Setup(s => s.OpenReadAsync(attachment.StorageKey, default)).ReturnsAsync(content);

        // Act
        var result = await _handler.Handle(new GetBriefingAttachmentContentQuery(briefing.Id, attachment.Id, Token), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Content = content, ContentType = "video/mp4", FileName = "clip.mp4" });
    }

    [Fact]
    public async Task Handle_WhenTokenIsInvalid_ReturnsNotFoundWithoutReadingTheBriefing()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new GetBriefingAttachmentContentQuery(briefingId, attachmentId, "forged"), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        _briefingRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenBriefingDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        _signerMock.Setup(s => s.Verify(briefingId, attachmentId, Token)).Returns(true);

        // Act
        var result = await _handler.Handle(new GetBriefingAttachmentContentQuery(briefingId, attachmentId, Token), default);

        // Assert
        result.Error.Should().Be(BriefingErrors.AttachmentNotFound);
    }

    [Fact]
    public async Task Handle_WhenAttachmentIsNoLongerOnTheBriefing_ReturnsNotFound()
    {
        // Arrange
        var (briefing, attachment) = BriefingWithVideo();
        briefing.RemoveAttachment(attachment.Id);

        // Act
        var result = await _handler.Handle(new GetBriefingAttachmentContentQuery(briefing.Id, attachment.Id, Token), default);

        // Assert
        result.Error.Should().Be(BriefingErrors.AttachmentNotFound);
    }

    [Fact]
    public async Task Handle_WhenStoredObjectIsMissing_ReturnsNotFound()
    {
        // Arrange
        var (briefing, attachment) = BriefingWithVideo();
        _fileStorageMock.Setup(s => s.OpenReadAsync(attachment.StorageKey, default)).ReturnsAsync((Stream?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingAttachmentContentQuery(briefing.Id, attachment.Id, Token), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }
}
