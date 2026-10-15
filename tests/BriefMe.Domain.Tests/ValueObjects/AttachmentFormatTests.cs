using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class AttachmentFormatTests
{
    public static TheoryData<byte[], string, AttachmentKind> SupportedHeaders => new()
    {
        { [.. "%PDF-1.7\n"u8], "application/pdf", AttachmentKind.Pdf },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00], "image/png", AttachmentKind.Image },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10], "image/jpeg", AttachmentKind.Image },
        { [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8], "image/webp", AttachmentKind.Image },
        { [.. "GIF87a"u8, 0x01, 0x00], "image/gif", AttachmentKind.Image },
        { [.. "GIF89a"u8, 0x01, 0x00], "image/gif", AttachmentKind.Image },
        { [.. "ID3"u8, 0x04, 0x00, 0x00], "audio/mpeg", AttachmentKind.Audio },
        { [0xFF, 0xFB, 0x90, 0x64], "audio/mpeg", AttachmentKind.Audio },
        { [0xFF, 0xF3, 0x90, 0x64], "audio/mpeg", AttachmentKind.Audio },
        { [0xFF, 0xE3, 0x90, 0x64], "audio/mpeg", AttachmentKind.Audio },
        { [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVE"u8, .. "fmt "u8], "audio/wav", AttachmentKind.Audio },
        { [.. "OggS"u8, 0x00, 0x02], "audio/ogg", AttachmentKind.Audio },
        { [0x00, 0x00, 0x00, 0x20, .. "ftyp"u8, .. "M4A "u8, 0x00, 0x00, 0x00, 0x00], "audio/mp4", AttachmentKind.Audio },
        { [0x00, 0x00, 0x00, 0x20, .. "ftyp"u8, .. "isom"u8, 0x00, 0x00, 0x02, 0x00], "video/mp4", AttachmentKind.Video },
        { [0x00, 0x00, 0x00, 0x18, .. "ftyp"u8, .. "mp42"u8, 0x00, 0x00, 0x00, 0x00], "video/mp4", AttachmentKind.Video },
        { [0x00, 0x00, 0x00, 0x1C, .. "ftyp"u8, .. "avc1"u8, 0x00, 0x00, 0x00, 0x00], "video/mp4", AttachmentKind.Video },
        { [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81], "video/webm", AttachmentKind.Video },
    };

    public static TheoryData<byte[]> UnsupportedHeaders => new()
    {
        { [] },
        { [0xFF] },
        { [.. "<html><body>"u8] },
        { [.. "<svg xmlns="u8] },
        { [.. "<?xml version"u8] },
        { [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00] }, // ZIP / DOCX
        { [0x4D, 0x5A, 0x90, 0x00] }, // Windows executable
        { [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "AVI "u8] },
        { [0x00, 0x00, 0x00, 0x14, .. "ftyp"u8, .. "qt  "u8, 0x00, 0x00, 0x00, 0x00] }, // MOV
        { [0x00, 0x00, 0x00, 0x18, .. "ftyp"u8, .. "heic"u8, 0x00, 0x00, 0x00, 0x00] }, // HEIC
        { [0xFF, 0xEB, 0x90, 0x64] }, // MPEG frame with the reserved version bits
    };

    [Theory]
    [MemberData(nameof(SupportedHeaders))]
    public void Detect_WhenSignatureIsSupported_ReturnsMatchingContentType(byte[] header, string contentType, AttachmentKind kind)
    {
        // Arrange
        // (header supplied by the theory)

        // Act
        var result = AttachmentFormat.Detect(header);

        // Assert
        result.Value.ContentType.Should().Be(contentType);
        result.Value.Kind.Should().Be(kind);
    }

    [Theory]
    [MemberData(nameof(UnsupportedHeaders))]
    public void Detect_WhenSignatureIsNotSupported_ReturnsFailure(byte[] header)
    {
        // Arrange
        // (header supplied by the theory)

        // Act
        var result = AttachmentFormat.Detect(header);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AttachmentFormat.UnsupportedFormatMessage);
    }

    [Fact]
    public void Equals_WhenSameContentType_ReturnsTrue()
    {
        // Arrange
        var detected = AttachmentFormat.Detect("%PDF-1.4"u8).Value;

        // Act
        var equal = detected.Equals(AttachmentFormat.Pdf);

        // Assert
        equal.Should().BeTrue();
    }
}
