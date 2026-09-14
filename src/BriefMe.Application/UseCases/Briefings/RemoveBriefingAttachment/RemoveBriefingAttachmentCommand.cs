using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.RemoveBriefingAttachment;

/// <summary>
/// Drops one reference file from a briefing (RF005, BRF-013). The endpoint behind it is
/// <c>DELETE /briefings/{id}/attachments/{attachmentId}</c> (BRF-012).
/// </summary>
/// <remarks>
/// Returns the whole <see cref="BriefingDetailDto"/> rather than nothing: the caller is a form that renders
/// the remaining list, and the aggregate is already loaded with its children for the removal itself.
/// </remarks>
public sealed record RemoveBriefingAttachmentCommand(
    Guid BriefingId,
    Guid AttachmentId) : ICommand<BriefingDetailDto>;
