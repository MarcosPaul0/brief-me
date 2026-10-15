using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Entities;

/// <summary>
/// A file the requester attached to a briefing. Part of the <see cref="Briefing"/> aggregate and created only
/// through <see cref="Briefing.AddAttachment"/>; immutable afterwards — a wrong file is removed and re-uploaded.
/// </summary>
public sealed class BriefingAttachment : Entity
{
    public const int FileNameMaxLength = 255;

    public const string FallbackFileName = "arquivo";

    private BriefingAttachment(
        Guid id,
        Guid briefingId,
        string fileName,
        string storageKey,
        AttachmentFormat format,
        long sizeBytes,
        DateTime uploadedAt) : base(id)
    {
        BriefingId = briefingId;
        FileName = fileName;
        StorageKey = storageKey;
        ContentType = format.ContentType;
        Kind = format.Kind;
        SizeBytes = sizeBytes;
        UploadedAt = uploadedAt;
    }

    private BriefingAttachment()
    {
    }

    public Guid BriefingId { get; private set; }

    /// <summary>The uploaded name, sanitised. Display only — never part of <see cref="StorageKey"/>.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Object key in file storage.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>The detected content type (see <see cref="AttachmentFormat"/>), never the one the client declared.</summary>
    public string ContentType { get; private set; } = string.Empty;

    public AttachmentKind Kind { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTime UploadedAt { get; private set; }

    internal static BriefingAttachment Create(
        Guid briefingId,
        string? fileName,
        string storageKey,
        AttachmentFormat format,
        long sizeBytes) =>
        new(Guid.NewGuid(), briefingId, SanitizeFileName(fileName), storageKey, format, sizeBytes, DateTime.UtcNow);

    /// <summary>
    /// Keeps the last path segment, drops control characters and trims; caps the length while keeping the
    /// extension visible. Blank results fall back to <see cref="FallbackFileName"/>.
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return FallbackFileName;

        var lastSegment = fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];
        var cleaned = new string(lastSegment.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (cleaned.Length == 0)
            return FallbackFileName;

        if (cleaned.Length <= FileNameMaxLength)
            return cleaned;

        var extension = Path.GetExtension(cleaned);
        if (extension.Length is 0 or > 16)
            return cleaned[..FileNameMaxLength];

        return cleaned[..(FileNameMaxLength - extension.Length)] + extension;
    }
}
