using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// One line of a briefing's tracking timeline (BRF-039, RF023): a task of the briefing as the requester or
/// the Design Manager reads it — what it is, who has it, when it is due and where it stands (RF020).
/// </summary>
/// <remarks>
/// Distinct from <see cref="DesignerTaskDto"/>, which is the same aggregate seen from the other side: the
/// board (BRF-033) lists one designer's tasks across briefings and names the parent briefing, while this
/// one lists one briefing's tasks across designers and names the assignee. Distinct from
/// <see cref="BriefingTaskDto"/>, the write use cases' echo, which knows only ids.
/// <para>
/// <see cref="DesignerId"/> is nullable because assignment is a separate act (RF017, BRF-027) and
/// <see cref="Deadline"/> because planning is another (RF018, BRF-028); <see cref="DesignerName"/> is
/// nullable on top of that because the lookup may not resolve — a soft-deleted user degrades one cell of
/// one row, never the page.
/// </para>
/// </remarks>
public sealed record BriefingTrackingTaskDto(
    Guid Id,
    string Title,
    Guid? DesignerId,
    string? DesignerName,
    DateOnly? Deadline,
    Priority Priority,
    BriefingTaskStatus Status)
{
    public static BriefingTrackingTaskDto FromBriefingTaskWithDesignerName(
        BriefingTask briefingTask,
        IReadOnlyDictionary<Guid, string> personNames)
    {
        return new(
            briefingTask.Id,
            briefingTask.Title,
            briefingTask.DesignerId,
            briefingTask.DesignerId is { } designerId ? personNames.GetValueOrDefault(designerId) : null,
            briefingTask.Deadline,
            briefingTask.Priority,
            briefingTask.Status);
    }
}
