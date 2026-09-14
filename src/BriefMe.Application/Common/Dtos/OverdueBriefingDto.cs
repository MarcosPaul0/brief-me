using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of a briefing the deadline monitor marked as late and that is still open (RF030, BRF-045) —
/// the row of the overdue demand list. Distinct from <see cref="PendingApprovalBriefingDto"/>, which is the
/// review queue: that one carries <c>CreatedAt</c> because what matters there is how long a briefing has
/// waited for a decision, while this one carries <see cref="OverdueSince"/> and spans every open status, not
/// just <c>AwaitingApproval</c>.
/// </summary>
/// <remarks>
/// <see cref="DesiredDeliveryDate"/> is nullable because a briefing can breach the SLA policy (RF027)
/// without the requester ever having typed a date (RF005). <see cref="RequesterName"/> and
/// <see cref="CategoryName"/> are resolved server-side and degrade to <c>null</c> alone if a lookup misses —
/// a removed requester or a deactivated category costs one cell, never the row.
/// <para>
/// How long the briefing has been late is deliberately absent: it is <see cref="OverdueSince"/> against the
/// clock, and a DTO factory is pure (<c>.claude/rules/dto-mapping.md</c>) — the screen formats the elapsed
/// time. Progress is absent for a different reason: it is a per-briefing task read (BRF-035), and this list
/// answers what ran late, not how far it got.
/// </para>
/// </remarks>
public sealed record OverdueBriefingDto(
    Guid Id,
    string Title,
    DateOnly? DesiredDeliveryDate,
    Priority Priority,
    BriefingStatus Status,
    Guid CategoryId,
    string? CategoryName,
    Guid RequesterId,
    string? RequesterName,
    DateTime OverdueSince)
{
    public static OverdueBriefingDto FromBriefingWithComplements(
        Briefing briefing,
        IReadOnlyDictionary<Guid, string> requesterNames,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        return new(
            briefing.Id,
            briefing.Title,
            briefing.DesiredDeliveryDate,
            briefing.Priority,
            briefing.Status,
            briefing.CategoryId,
            categoryNames.GetValueOrDefault(briefing.CategoryId),
            briefing.RequesterId,
            requesterNames.GetValueOrDefault(briefing.RequesterId),
            // Only marked briefings reach this factory — the query that feeds it requires the mark (BRF-045).
            briefing.OverdueSince!.Value);
    }
}
