using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class AvatarFormatTests
{
    [Fact]
    public void Detect_WhenBytesArePng_ReturnsPng()
    {
        // Arrange
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

        // Act
        var result = AvatarFormat.Detect(header);

        // Assert
        result.Value.Should().Be(AvatarFormat.Png);
    }

    [Fact]
    public void Detect_WhenBytesAreJpeg_ReturnsJpeg()
    {
        // Arrange
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        // Act
        var result = AvatarFormat.Detect(header);

        // Assert
        result.Value.Should().Be(AvatarFormat.Jpeg);
    }

    [Fact]
    public void Detect_WhenBytesAreWebP_ReturnsWebP()
    {
        // Arrange
        byte[] header = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

        // Act
        var result = AvatarFormat.Detect(header);

        // Assert
        result.Value.Should().Be(AvatarFormat.WebP);
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // GIF89a
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E })] // <html>
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 })] // RIFF....WAVE
    [InlineData(new byte[] { 0x89, 0x50 })] // truncated PNG
    [InlineData(new byte[] { })]
    public void Detect_WhenBytesAreNotASupportedImage_ReturnsFailure(byte[] header)
    {
        // Arrange
        // (header from InlineData)

        // Act
        var result = AvatarFormat.Detect(header);

        // Assert
        result.Error.Should().Be(AvatarFormat.InvalidFormatMessage);
    }
}
