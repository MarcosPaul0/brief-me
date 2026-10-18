using BriefMe.Application.Common.Briefings;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// A single briefing with the names the detail screen shows next to its ids. The names are nullable
/// because a lookup can miss (a removed user, say) — the screen then shows a dash instead of failing.
/// <c>Attachments</c> are mapped by the handler, which owns the token signing, and passed in ready.
/// </summary>
public sealed record BriefingDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? Objective,
    string? RequestingArea,
    DateOnly? DesiredDeliveryDate,
    Priority Priority,
    Guid CategoryId,
    string? CategoryName,
    Guid RequesterId,
    string? RequesterName,
    Guid? DesignerId,
    string? DesignerName,
    BriefingStatus Status,
    string? DeliveryNote,
    DateTime? DeliveredAt,
    string? ChangeRequestReason,
    DateTime CreatedAt,
    IReadOnlyList<BriefingAttachmentDto> Attachments)
{
    public static BriefingDetailDto FromBriefingWithComplements(
        Briefing briefing,
        BriefingComplements complements,
        IReadOnlyList<BriefingAttachmentDto> attachments)
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
            complements.CategoryName(briefing.CategoryId),
            briefing.RequesterId,
            complements.UserName(briefing.RequesterId),
            briefing.DesignerId,
            complements.UserName(briefing.DesignerId),
            briefing.Status,
            briefing.DeliveryNote,
            briefing.DeliveredAt,
            briefing.ChangeRequestReason?.Value,
            briefing.CreatedAt,
            attachments);
    }
}
