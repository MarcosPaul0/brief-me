using BriefMe.Application.Common.Briefings;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// One row of the briefing lists — the requester's own, the designer queue and the designer's work. Names are
/// resolved server-side and nullable for the same reason as on <see cref="BriefingDetailDto"/>.
/// </summary>
public sealed record BriefingListItemDto(
    Guid Id,
    string Title,
    Priority Priority,
    BriefingStatus Status,
    DateOnly? DesiredDeliveryDate,
    Guid CategoryId,
    string? CategoryName,
    string? RequesterName,
    string? DesignerName,
    DateTime CreatedAt)
{
    public static BriefingListItemDto FromBriefingWithComplements(Briefing briefing, BriefingComplements complements)
    {
        return new(
            briefing.Id,
            briefing.Title,
            briefing.Priority,
            briefing.Status,
            briefing.DesiredDeliveryDate,
            briefing.CategoryId,
            complements.CategoryName(briefing.CategoryId),
            complements.UserName(briefing.RequesterId),
            complements.UserName(briefing.DesignerId),
            briefing.CreatedAt);
    }
}
