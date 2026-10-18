using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// One attachment as the detail screen shows it. <c>ContentToken</c> is a short-lived signed token for the
/// content endpoint (ATT-18); the UI builds the URL from it, so this layer never knows HTTP routes.
/// </summary>
public sealed record BriefingAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    AttachmentKind Kind,
    long SizeBytes,
    DateTime UploadedAt,
    string ContentToken)
{
    public static BriefingAttachmentDto FromAttachmentAndToken(BriefingAttachment attachment, string contentToken)
    {
        return new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Kind,
            attachment.SizeBytes,
            attachment.UploadedAt,
            contentToken);
    }
}
