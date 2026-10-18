using BriefMe.Domain.Entities;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class BriefingAttachmentTests
{
    [Theory]
    [InlineData("logo.png", "logo.png")]
    [InlineData("  brand guide.pdf  ", "brand guide.pdf")]
    [InlineData("C:\\Users\\ana\\Desktop\\ref.jpg", "ref.jpg")]
    [InlineData("../../etc/passwd.mp3", "passwd.mp3")]
    [InlineData("voice\u0000over\u001f.wav", "voiceover.wav")]
    [InlineData(null, BriefingAttachment.FallbackFileName)]
    [InlineData("", BriefingAttachment.FallbackFileName)]
    [InlineData("   ", BriefingAttachment.FallbackFileName)]
    [InlineData("folder/", BriefingAttachment.FallbackFileName)]
    public void SanitizeFileName_WhenGivenUploadedName_ReturnsDisplaySafeName(string? fileName, string expected)
    {
        // Arrange
        // (input supplied by the theory)

        // Act
        var sanitized = BriefingAttachment.SanitizeFileName(fileName);

        // Assert
        sanitized.Should().Be(expected);
    }

    [Fact]
    public void SanitizeFileName_WhenNameIsTooLong_TruncatesKeepingExtension()
    {
        // Arrange
        var fileName = new string('a', 300) + ".webm";

        // Act
        var sanitized = BriefingAttachment.SanitizeFileName(fileName);

        // Assert
        sanitized.Should().HaveLength(BriefingAttachment.FileNameMaxLength).And.EndWith(".webm");
    }
}
