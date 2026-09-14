using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of a briefing waiting for the Design Manager's review (BRF-020, RF011–RF014). Distinct from
/// <see cref="BriefingDto"/>: the reviewer looks at someone else's request, so the raw
/// <c>RequesterId</c>/<c>CategoryId</c> carry no meaning on screen and the display names are resolved
/// server-side. <see cref="RequesterName"/> and <see cref="CategoryName"/> are nullable on purpose — a
/// deleted requester or a deactivated category degrades that one cell, never the whole queue.
/// </summary>
public sealed record PendingApprovalBriefingDto(
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
    BriefingStatus Status,
    DateTime CreatedAt)
{
    public static PendingApprovalBriefingDto FromBriefingWithComplements(
        Briefing briefing,
        IReadOnlyDictionary<Guid, string> requesterNames,
        IReadOnlyDictionary<Guid, string> categoryNames)
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
            categoryNames.GetValueOrDefault(briefing.CategoryId),
            briefing.RequesterId,
            requesterNames.GetValueOrDefault(briefing.RequesterId),
            briefing.Status,
            briefing.CreatedAt);
    }
}
