using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.Common.Briefings;

public class BriefingAttachmentStagerTests
{
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly BriefingAttachmentStager _stager;
    private readonly Guid _briefingId = Guid.NewGuid();

    public BriefingAttachmentStagerTests()
    {
        _stager = new BriefingAttachmentStager(_fileStorageMock.Object);
    }

    [Fact]
    public async Task StageAsync_WhenAllFilesAreValid_ReturnsOneStagedAttachmentPerFile()
    {
        // Arrange
        var uploads = new[] { TestUploads.Pdf("brief.pdf"), TestUploads.Mp4("clip.mp4") };

        // Act
        var result = await _stager.StageAsync(_briefingId, uploads, default);

        // Assert
        result.Value.Select(s => (s.FileName, s.Format)).Should().Equal(
            ("brief.pdf", AttachmentFormat.Pdf),
            ("clip.mp4", AttachmentFormat.Mp4));
    }

    [Fact]
    public async Task StageAsync_WhenFileIsValid_SavesUnderBriefingKeyWithDetectedContentType()
    {
        // Arrange
        var upload = TestUploads.Png("whatever.pdf");

        // Act
        var result = await _stager.StageAsync(_briefingId, [upload], default);

        // Assert
        result.Value.Single().StorageKey.Should().StartWith($"briefings/{_briefingId:N}/").And.EndWith(".png");
        _fileStorageMock.Verify(s => s.SaveAsync(result.Value.Single().StorageKey, upload.Content, "image/png", default), Times.Once);
    }

    [Fact]
    public async Task StageAsync_WhenFileIsValid_SavesStreamRewoundToStart()
    {
        // Arrange
        var upload = TestUploads.Pdf();
        long? positionAtSave = null;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), upload.Content, It.IsAny<string>(), default))
            .Callback<string, Stream, string, CancellationToken>((_, stream, _, _) => positionAtSave = stream.Position);

        // Act
        await _stager.StageAsync(_briefingId, [upload], default);

        // Assert
        positionAtSave.Should().Be(0);
    }

    [Fact]
    public async Task StageAsync_WhenOneFileHasUnsupportedFormat_FailsNamingItAndStoresNothing()
    {
        // Arrange
        var uploads = new[] { TestUploads.Pdf(), TestUploads.Html("evil.png") };

        // Act
        var result = await _stager.StageAsync(_briefingId, uploads, default);

        // Assert
        result.Error.Should().Be($"evil.png: {AttachmentFormat.UnsupportedFormatMessage}");
        _fileStorageMock.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_WhenFileIsEmpty_FailsAndStoresNothing()
    {
        // Arrange
        var uploads = new[] { TestUploads.Pdf(), TestUploads.Empty("empty.pdf") };

        // Act
        var result = await _stager.StageAsync(_briefingId, uploads, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _fileStorageMock.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_WhenFileIsOversized_FailsAndStoresNothing()
    {
        // Arrange
        var uploads = new[] { TestUploads.Oversized() };

        // Act
        var result = await _stager.StageAsync(_briefingId, uploads, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _fileStorageMock.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_WhenASaveThrows_DeletesFilesAlreadyStoredAndRethrows()
    {
        // Arrange
        var first = TestUploads.Pdf("first.pdf");
        var second = TestUploads.Png("second.png");
        string? firstKey = null;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), first.Content, It.IsAny<string>(), default))
            .Callback<string, Stream, string, CancellationToken>((key, _, _, _) => firstKey = key);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), second.Content, It.IsAny<string>(), default))
            .ThrowsAsync(new IOException("storage down"));

        // Act
        var act = () => _stager.StageAsync(_briefingId, [first, second], default);

        // Assert
        await act.Should().ThrowAsync<IOException>();
        _fileStorageMock.Verify(s => s.DeleteAsync(firstKey!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAsync_WhenNoFiles_SucceedsWithoutTouchingStorage()
    {
        // Arrange
        // (no uploads)

        // Act
        var result = await _stager.StageAsync(_briefingId, [], default);

        // Assert
        result.Value.Should().BeEmpty();
        _fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DiscardAsync_WhenGivenStagedAttachments_DeletesEachKey()
    {
        // Arrange
        var staged = new[]
        {
            new StagedAttachment("a.pdf", "briefings/x/a.pdf", AttachmentFormat.Pdf, 10),
            new StagedAttachment("b.png", "briefings/x/b.png", AttachmentFormat.Png, 10),
        };

        // Act
        await _stager.DiscardAsync(staged, default);

        // Assert
        _fileStorageMock.Verify(s => s.DeleteAsync("briefings/x/a.pdf", default), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteAsync("briefings/x/b.png", default), Times.Once);
    }
}
