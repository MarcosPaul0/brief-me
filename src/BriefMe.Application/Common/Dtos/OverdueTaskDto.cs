using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of a task the deadline monitor marked as late and that is still open (RF029, BRF-044) — the
/// row of the overdue list. Distinct from <see cref="DesignerTaskDto"/>, which is the assignee's own work
/// plan: this one is read across designers by whoever answers for the delay, so the assignee is part of the
/// row rather than implied by it, and <see cref="OverdueSince"/> travels with it.
/// </summary>
/// <remarks>
/// <see cref="Deadline"/> is nullable because a task can be late against the SLA policy (RF027) without ever
/// having had a typed deadline (RF018). <see cref="DesignerId"/> and <see cref="DesignerName"/> are nullable
/// because assignment (RF017) is a separate act — an unassigned task that is already late is exactly what the
/// Design Manager most needs to see. <see cref="BriefingTitle"/> and <see cref="DesignerName"/> are resolved
/// server-side and degrade to <c>null</c> alone if a lookup misses; the row stays usable.
/// <para>
/// How long the task has been late is deliberately absent: it is <see cref="OverdueSince"/> against the
/// clock, and a DTO factory is pure (<c>.claude/rules/dto-mapping.md</c>) — the screen formats the elapsed
/// time.
/// </para>
/// </remarks>
public sealed record OverdueTaskDto(
    Guid Id,
    Guid BriefingId,
    string? BriefingTitle,
    string Title,
    DateOnly? Deadline,
    Priority Priority,
    BriefingTaskStatus Status,
    Guid? DesignerId,
    string? DesignerName,
    DateTime OverdueSince)
{
    public static OverdueTaskDto FromBriefingTaskWithComplements(
        BriefingTask briefingTask,
        IReadOnlyDictionary<Guid, string> briefingTitles,
        IReadOnlyDictionary<Guid, string> designerNames)
    {
        return new(
            briefingTask.Id,
            briefingTask.BriefingId,
            briefingTitles.GetValueOrDefault(briefingTask.BriefingId),
            briefingTask.Title,
            briefingTask.Deadline,
            briefingTask.Priority,
            briefingTask.Status,
            briefingTask.DesignerId,
            briefingTask.DesignerId is null ? null : designerNames.GetValueOrDefault(briefingTask.DesignerId.Value),
            // Only marked tasks reach this factory — the query that feeds it requires the mark (BRF-044).
            briefingTask.OverdueSince!.Value);
    }
}
