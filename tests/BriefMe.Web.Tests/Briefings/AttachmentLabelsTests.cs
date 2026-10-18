using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;
using BriefMe.Web.Components.Briefings;
using FluentAssertions;

namespace BriefMe.Web.Tests.Briefings;

public class AttachmentLabelsTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1536L, "1,5 KB")]
    [InlineData(2_621_440L, "2,5 MB")]
    [InlineData(26_214_400L, "25 MB")]
    public void Size_WhenGivenBytes_FormatsInPortuguese(long bytes, string expected)
    {
        // Arrange
        // (bytes from InlineData)

        // Act
        var label = AttachmentLabels.Size(bytes);

        // Assert
        label.Should().Be(expected);
    }

    [Theory]
    [InlineData("guia.pdf", AttachmentKind.Pdf)]
    [InlineData("LOGO.PNG", AttachmentKind.Image)]
    [InlineData("foto.jpeg", AttachmentKind.Image)]
    [InlineData("foto.jpg", AttachmentKind.Image)]
    [InlineData("arte.webp", AttachmentKind.Image)]
    [InlineData("anim.gif", AttachmentKind.Image)]
    [InlineData("locucao.mp3", AttachmentKind.Audio)]
    [InlineData("trilha.wav", AttachmentKind.Audio)]
    [InlineData("nota.ogg", AttachmentKind.Audio)]
    [InlineData("nota.oga", AttachmentKind.Audio)]
    [InlineData("voz.m4a", AttachmentKind.Audio)]
    [InlineData("ref.mp4", AttachmentKind.Video)]
    [InlineData("ref.webm", AttachmentKind.Video)]
    public void GuessKind_WhenExtensionIsAccepted_ReturnsKind(string fileName, AttachmentKind expected)
    {
        // Arrange
        // (file name from InlineData)

        // Act
        var kind = AttachmentLabels.GuessKind(fileName);

        // Assert
        kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("video.mov")]
    [InlineData("foto.heic")]
    [InlineData("vetor.svg")]
    [InlineData("sem-extensao")]
    [InlineData(null)]
    public void GuessKind_WhenExtensionIsNotAccepted_ReturnsNull(string? fileName)
    {
        // Arrange
        // (file name from InlineData)

        // Act
        var kind = AttachmentLabels.GuessKind(fileName);

        // Assert
        kind.Should().BeNull();
    }

    [Fact]
    public void ContentUrl_WhenTokenHasReservedCharacters_EscapesIt()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var attachment = new BriefingAttachmentDto(Guid.NewGuid(), "a.pdf", "application/pdf", AttachmentKind.Pdf, 1, DateTime.UtcNow, "123.a+b/c=");

        // Act
        var url = AttachmentLabels.ContentUrl(briefingId, attachment);

        // Assert
        url.Should().Be($"briefings/{briefingId}/attachments/{attachment.Id}/content?token=123.a%2Bb%2Fc%3D");
    }
}
