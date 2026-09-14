using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Output representation of a briefing for outer layers (BRF-006). Never exposes the domain entity directly.
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
    BriefingStatus Status)
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
            briefing.Status);
    }
}
