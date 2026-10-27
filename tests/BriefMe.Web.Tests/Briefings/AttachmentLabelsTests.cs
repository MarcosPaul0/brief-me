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

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(65, "01:05")]
    [InlineData(600, "10:00")]
    public void Duration_WhenGivenSeconds_FormatsAsMinutesAndSeconds(int seconds, string expected)
    {
        // Arrange
        // (seconds from InlineData)

        // Act
        var label = AttachmentLabels.Duration(seconds);

        // Assert
        label.Should().Be(expected);
    }

    [Fact]
    public void RecordingFileName_WhenNameIsFree_UsesTheStartTimestamp()
    {
        // Arrange
        string[] taken = ["guia.pdf"];

        // Act
        var name = AttachmentLabels.RecordingFileName("2026-10-26-143005", taken);

        // Assert
        name.Should().Be("gravacao-2026-10-26-143005.wav");
    }

    [Fact]
    public void RecordingFileName_WhenSameSecondIsTaken_AddsTheNextFreeSuffix()
    {
        // Arrange
        string[] taken = ["gravacao-2026-10-26-143005.wav", "GRAVACAO-2026-10-26-143005-2.wav"];

        // Act
        var name = AttachmentLabels.RecordingFileName("2026-10-26-143005", taken);

        // Assert
        name.Should().Be("gravacao-2026-10-26-143005-3.wav");
    }

    [Fact]
    public void GroupByKind_WhenKindsAreMixed_OrdersGroupsImagesVideosPdfsAudios()
    {
        // Arrange
        BriefingAttachmentDto[] attachments =
        [
            Attachment("a.mp3", AttachmentKind.Audio),
            Attachment("b.pdf", AttachmentKind.Pdf),
            Attachment("c.png", AttachmentKind.Image),
            Attachment("d.mp4", AttachmentKind.Video),
        ];

        // Act
        var groups = AttachmentLabels.GroupByKind(attachments);

        // Assert
        groups.Select(g => g.Key).Should().Equal(AttachmentKind.Image, AttachmentKind.Video, AttachmentKind.Pdf, AttachmentKind.Audio);
    }

    [Fact]
    public void GroupByKind_WhenKindIsAbsent_OmitsItsGroup()
    {
        // Arrange
        BriefingAttachmentDto[] attachments = [Attachment("a.pdf", AttachmentKind.Pdf), Attachment("b.png", AttachmentKind.Image)];

        // Act
        var groups = AttachmentLabels.GroupByKind(attachments);

        // Assert
        groups.Select(g => g.Key).Should().Equal(AttachmentKind.Image, AttachmentKind.Pdf);
    }

    [Fact]
    public void GroupByKind_WhenGroupHasSeveralItems_KeepsTheirOriginalOrder()
    {
        // Arrange
        BriefingAttachmentDto[] attachments =
        [
            Attachment("primeira.png", AttachmentKind.Image),
            Attachment("guia.pdf", AttachmentKind.Pdf),
            Attachment("segunda.png", AttachmentKind.Image),
        ];

        // Act
        var groups = AttachmentLabels.GroupByKind(attachments);

        // Assert
        groups[0].Select(a => a.FileName).Should().Equal("primeira.png", "segunda.png");
    }

    [Fact]
    public void GroupByKind_WhenKindIsUnknown_PlacesItsGroupLast()
    {
        // Arrange
        BriefingAttachmentDto[] attachments = [Attachment("x.bin", (AttachmentKind)99), Attachment("a.mp3", AttachmentKind.Audio)];

        // Act
        var groups = AttachmentLabels.GroupByKind(attachments);

        // Assert
        groups.Select(g => g.Key).Should().Equal(AttachmentKind.Audio, (AttachmentKind)99);
    }

    [Theory]
    [InlineData(AttachmentKind.Image, "Imagens")]
    [InlineData(AttachmentKind.Video, "Vídeos")]
    [InlineData(AttachmentKind.Pdf, "PDFs")]
    [InlineData(AttachmentKind.Audio, "Áudios")]
    public void KindPlural_WhenGivenKind_ReturnsGroupTitle(AttachmentKind kind, string expected)
    {
        // Arrange
        // (kind from InlineData)

        // Act
        var label = AttachmentLabels.KindPlural(kind);

        // Assert
        label.Should().Be(expected);
    }

    private static BriefingAttachmentDto Attachment(string fileName, AttachmentKind kind) =>
        new(Guid.NewGuid(), fileName, "application/octet-stream", kind, 1, DateTime.UtcNow, "token");
}
