using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Output representation of a file attached to a briefing (RF005) or delivered on one of its tasks
/// (RF022, BRF-031). Carries the author and the moment because that is what RF052 asks a delivery to
/// record — a correction is a new attachment, never an edit (BRF-004), so the reader needs to know who
/// uploaded what and when.
/// </summary>
/// <remarks>
/// <c>TaskId</c> is null on a briefing attachment and set on a task delivery — the same nullable column that
/// separates the two kinds of row in the table.
/// <para>
/// <c>StoragePath</c> is deliberately absent: the storage key is an opaque internal detail of
/// <c>IFileStorageService</c> (BRF-011), and exposing it would leak the storage layout and invite consumers
/// to build URLs from it. Downloading is by attachment id, through the endpoint that can authorize it
/// (BRF-032, BRF-077).
/// </para>
/// <para>
/// <c>UploadedById</c> is an id, not a name: resolving display names is the reading side's job (BRF-034),
/// which loads a page of attachments at once and can look the users up in one query — the same shape as
/// <c>CommentDto</c>.
/// </para>
/// </remarks>
public sealed record AttachmentDto(
    Guid Id,
    Guid BriefingId,
    Guid? TaskId,
    string FileName,
    long SizeBytes,
    Guid UploadedById,
    DateTime UploadedAt)
{
    public static AttachmentDto FromAttachment(Attachment attachment)
    {
        return new(
            attachment.Id,
            attachment.BriefingId,
            attachment.TaskId,
            attachment.FileName,
            attachment.SizeBytes,
            attachment.UploadedById,
            attachment.UploadedAt);
    }
}
