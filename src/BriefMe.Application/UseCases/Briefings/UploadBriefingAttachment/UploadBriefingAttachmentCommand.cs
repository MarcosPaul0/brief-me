using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;

/// <summary>
/// Attaches a reference file to a briefing (RF005, BRF-013). The endpoint behind it is
/// <c>POST /briefings/{id}/attachments</c> (BRF-012).
/// </summary>
/// <remarks>
/// The uploader is not part of the command: it is the authenticated user (<c>ICurrentUser</c>), whom the
/// handler records as the file's author. The RF022 counterpart is <c>SubmitTaskDeliveryCommand</c>, and the
/// two share their shape for the same reasons: <see cref="Content"/> is a <see cref="Stream"/> because that
/// is what <c>IFileStorageService</c> takes (BRF-011), and <see cref="SizeBytes"/> travels alongside it
/// because <c>IFileValidator</c> is defined over a declared size and the stream may not be seekable. Both are
/// nullable/unchecked here — an absent multipart part is what the validator turns into a <c>400</c> instead
/// of an <c>ArgumentNullException</c>.
/// </remarks>
public sealed record UploadBriefingAttachmentCommand(
    Guid BriefingId,
    string? FileName,
    long SizeBytes,
    Stream? Content) : ICommand<AttachmentDto>;
