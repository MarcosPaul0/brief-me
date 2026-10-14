using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// A briefing as returned by every command that changes one. <see cref="DesignerId"/>, the delivery fields
/// and <see cref="ChangeRequestReason"/> are null until the briefing reaches the stage that fills them.
/// </summary>
public sealed record BriefingDto(
    Guid Id,
    string Title,
    string? Description,
    string? Objective,
    string? RequestingArea,
    DateOnly? DesiredDeliveryDate,
    Priority Priority,
    Guid CategoryId,
    Guid RequesterId,
    Guid? DesignerId,
    BriefingStatus Status,
    string? DeliveryNote,
    DateTime? DeliveredAt,
    string? ChangeRequestReason)
{
    public static BriefingDto FromBriefing(Briefing briefing)
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
            briefing.DesignerId,
            briefing.Status,
            briefing.DeliveryNote,
            briefing.DeliveredAt,
            briefing.ChangeRequestReason?.Value);
    }
}
