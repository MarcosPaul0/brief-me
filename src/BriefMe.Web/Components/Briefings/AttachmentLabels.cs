using System.Globalization;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Web.Ui;

namespace BriefMe.Web.Components.Briefings;

/// <summary>
/// Presentation helpers for briefing attachments: the glyph and label per kind, human sizes, the content URL
/// built from the signed token, and the client-side filter used by the picker. The filter is only a hint for
/// the user — the API detects the real format from the bytes.
/// </summary>
public static class AttachmentLabels
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Dictionary<string, AttachmentKind> KindByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = AttachmentKind.Pdf,
        [".png"] = AttachmentKind.Image,
        [".jpg"] = AttachmentKind.Image,
        [".jpeg"] = AttachmentKind.Image,
        [".webp"] = AttachmentKind.Image,
        [".gif"] = AttachmentKind.Image,
        [".mp3"] = AttachmentKind.Audio,
        [".wav"] = AttachmentKind.Audio,
        [".ogg"] = AttachmentKind.Audio,
        [".oga"] = AttachmentKind.Audio,
        [".m4a"] = AttachmentKind.Audio,
        [".mp4"] = AttachmentKind.Video,
        [".webm"] = AttachmentKind.Video,
    };

    /// <summary>The <c>accept</c> filter for the file picker.</summary>
    public static readonly string Accept = string.Join(',', KindByExtension.Keys);

    public static readonly string MaxSizeLabel = $"{AttachmentFormat.MaxSizeBytes / (1024 * 1024)} MB";

    public const string AcceptedFormatsLabel = "PDF, imagens (PNG, JPEG, WebP, GIF), áudio (MP3, WAV, OGG, M4A) e vídeo (MP4, WebM)";

    public static IconName Icon(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Image => IconName.Image,
        AttachmentKind.Audio => IconName.Music,
        AttachmentKind.Video => IconName.Video,
        _ => IconName.FileText
    };

    public static string Kind(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Pdf => "PDF",
        AttachmentKind.Image => "Imagem",
        AttachmentKind.Audio => "Áudio",
        AttachmentKind.Video => "Vídeo",
        _ => kind.ToString()
    };

    /// <summary>Title of a kind's row in the gallery.</summary>
    public static string KindPlural(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Pdf => "PDFs",
        AttachmentKind.Image => "Imagens",
        AttachmentKind.Audio => "Áudios",
        AttachmentKind.Video => "Vídeos",
        _ => kind.ToString()
    };

    /// <summary>
    /// One group per kind present, in gallery order (images, videos, PDFs, audio; an unknown kind last), each
    /// keeping the list's original order.
    /// </summary>
    public static IReadOnlyList<IGrouping<AttachmentKind, BriefingAttachmentDto>> GroupByKind(IEnumerable<BriefingAttachmentDto> attachments) =>
        [.. attachments.GroupBy(a => a.Kind).OrderBy(g => GalleryOrder(g.Key))];

    private static int GalleryOrder(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Image => 0,
        AttachmentKind.Video => 1,
        AttachmentKind.Pdf => 2,
        AttachmentKind.Audio => 3,
        _ => 4
    };

    /// <summary>Bytes as the UI shows them: "512 B", "1,5 KB", "2,4 MB".</summary>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{(bytes / 1024d).ToString("0.#", PtBr)} KB",
        _ => $"{(bytes / (1024d * 1024d)).ToString("0.#", PtBr)} MB"
    };

    /// <summary>Elapsed or total recording time as the recorder shows it: "00:05", "10:00".</summary>
    public static string Duration(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    /// <summary>
    /// File name for a microphone take started at <paramref name="startedAt"/> (browser-local "yyyy-MM-dd-HHmmss"),
    /// suffixed "-2", "-3"... when a queued file already has it — two takes in the same second stay apart.
    /// </summary>
    public static string RecordingFileName(string startedAt, IReadOnlyCollection<string> takenNames)
    {
        var baseName = $"gravacao-{startedAt}";
        var name = $"{baseName}.wav";

        for (var suffix = 2; takenNames.Contains(name, StringComparer.OrdinalIgnoreCase); suffix++)
            name = $"{baseName}-{suffix}.wav";

        return name;
    }

    /// <summary>Relative URL of the anonymous content endpoint; the token is the authorization (ATT-18).</summary>
    public static string ContentUrl(Guid briefingId, BriefingAttachmentDto attachment) =>
        $"briefings/{briefingId}/attachments/{attachment.Id}/content?token={Uri.EscapeDataString(attachment.ContentToken)}";

    /// <summary>The kind a picked file will most likely have, from its extension; null when it is not accepted.</summary>
    public static AttachmentKind? GuessKind(string? fileName) =>
        KindByExtension.TryGetValue(Path.GetExtension(fileName ?? string.Empty), out var kind) ? kind : null;
}
