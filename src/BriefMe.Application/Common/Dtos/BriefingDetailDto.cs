using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// A single briefing read with its own attachments (RF005) — what the create/edit screen needs to render the
/// form <b>and</b> the file list in one round trip. A superset of <see cref="BriefingDto"/>, which stays the
/// return of every command: making that one carry attachments would force ten handlers to load a collection
/// their callers never read.
/// </summary>
/// <remarks>
/// <see cref="Attachments"/> holds briefing material only. Task deliveries live in the same table and hang off
/// the same aggregate, but they belong to the task screen (BRF-034) — the repository read behind this DTO
/// filters them out, so an empty list here means "no reference files", never "no deliveries".
/// </remarks>
public sealed record BriefingDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? Objective,
    string? RequestingArea,
    DateOnly? DesiredDeliveryDate,
    Priority Priority,
    Guid CategoryId,
    Guid RequesterId,
    BriefingStatus Status,
    IReadOnlyList<AttachmentDto> Attachments)
{
    /// <summary>
    /// Maps an aggregate loaded through <c>IBriefingRepository.GetByIdWithAttachmentsAsync</c>. The
    /// attachments are the briefing's own children, not supplementary lookup data, so they are read off the
    /// entity rather than passed in — the factory stays pure either way.
    /// </summary>
    public static BriefingDetailDto FromBriefingWithAttachments(Briefing briefing)
    {
        return new(
            briefing.Id,
            briefing.Title,
            briefing.Description,
            briefing.Objective,
            briefing.RequestingArea,
            briefing.DesiredDeliveryDate,
            briefing.Priority,
            briefing.CategoryId,
            briefing.RequesterId,
            briefing.Status,
            [.. briefing.Attachments.Select(AttachmentDto.FromAttachment)]);
    }
}
