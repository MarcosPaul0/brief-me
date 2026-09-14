using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of a file delivered on a task as the detail screen renders it (RF022, RF052, BRF-034).
/// Carries the resolved name of who uploaded it next to the moment they did — that pairing is exactly what
/// RF052 asks a delivery to show, and it is why this read model exists instead of reusing
/// <see cref="AttachmentDto"/>, the write echo whose <c>UploadedById</c> is the caller's own id.
/// </summary>
/// <remarks>
/// <see cref="UploadedByName"/> is nullable: a removed or soft-deleted uploader degrades that one cell,
/// never the list. The storage key stays absent for the same reason it is absent from
/// <see cref="AttachmentDto"/> — it is an internal detail of <c>IFileStorageService</c> (BRF-011), and
/// downloading is by id through the endpoint that can authorize it (BRF-077).
/// </remarks>
public sealed record TaskDeliveryDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    Guid UploadedById,
    string? UploadedByName,
    DateTime UploadedAt)
{
    public static TaskDeliveryDto FromAttachmentWithUploaderName(
        Attachment attachment,
        IReadOnlyDictionary<Guid, string> uploaderNames)
    {
        return new(
            attachment.Id,
            attachment.FileName,
            attachment.SizeBytes,
            attachment.UploadedById,
            uploaderNames.GetValueOrDefault(attachment.UploadedById),
            attachment.UploadedAt);
    }
}
