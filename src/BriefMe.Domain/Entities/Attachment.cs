using BriefMe.Domain.Common;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

/// <summary>
/// Metadata of a file attached to a <see cref="Briefing"/> (RF005 — "Attachments") or produced as a task
/// delivery (RF022). Immutable once created: a new file version produces a new <see cref="Attachment"/>,
/// never an edit (BRF-004). Child entity of the <see cref="Briefing"/> aggregate — a delivery also carries
/// a <see cref="TaskId"/>, but ownership stays with the briefing so no entity has two aggregate roots.
/// </summary>
public class Attachment : Entity
{
    /// <summary>Width of the <c>FileName</c> column. Guarded here so an oversized name is a domain failure
    /// rather than a truncation or a <c>DbUpdateException</c> surfacing as a 500.</summary>
    public const int MaxFileNameLength = 255;

    private Attachment(
        Guid id,
        Guid briefingId,
        Guid? taskId,
        string fileName,
        string storagePath,
        long sizeBytes,
        Guid uploadedById,
        DateTime uploadedAt) : base(id)
    {
        BriefingId = briefingId;
        TaskId = taskId;
        FileName = fileName;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
        UploadedById = uploadedById;
        UploadedAt = uploadedAt;
    }

    private Attachment()
    {
    }

    public Guid BriefingId { get; private set; }

    /// <summary>
    /// Task this file was delivered for (RF022). Null on briefing attachments (RF005), which is what
    /// separates the two kinds of row in the same table.
    /// </summary>
    public Guid? TaskId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string StoragePath { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public Guid UploadedById { get; private set; }

    public DateTime UploadedAt { get; private set; }

    /// <summary>Reference material attached to the briefing itself (RF005). Carries no task.</summary>
    public static Attachment Create(
        Guid briefingId,
        string fileName,
        string storagePath,
        long sizeBytes,
        Guid uploadedById) =>
        CreateCore(briefingId, taskId: null, fileName, storagePath, sizeBytes, uploadedById);

    /// <summary>
    /// A file produced while executing a task (RF022), linked to both the task and its briefing. Author and
    /// timestamp are recorded here (RF052); the numbered version history arrives with BRF-074.
    /// </summary>
    public static Attachment CreateDelivery(
        Guid briefingId,
        Guid taskId,
        string fileName,
        string storagePath,
        long sizeBytes,
        Guid uploadedById)
    {
        if (taskId == Guid.Empty)
            throw new DomainException("Delivery task is required.");

        return CreateCore(briefingId, taskId, fileName, storagePath, sizeBytes, uploadedById);
    }

    private static Attachment CreateCore(
        Guid briefingId,
        Guid? taskId,
        string fileName,
        string storagePath,
        long sizeBytes,
        Guid uploadedById)
    {
        if (briefingId == Guid.Empty)
            throw new DomainException("Attachment briefing is required.");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("File name is required.");

        var normalizedFileName = fileName.Trim();

        if (normalizedFileName.Length > MaxFileNameLength)
            throw new DomainException($"File name must be at most {MaxFileNameLength} characters.");

        if (string.IsNullOrWhiteSpace(storagePath))
            throw new DomainException("Storage path is required.");

        if (sizeBytes <= 0)
            throw new DomainException("File size must be greater than zero.");

        if (uploadedById == Guid.Empty)
            throw new DomainException("Attachment author is required.");

        return new Attachment(
            Guid.NewGuid(),
            briefingId,
            taskId,
            normalizedFileName,
            storagePath.Trim(),
            sizeBytes,
            uploadedById,
            DateTime.UtcNow);
    }
}
