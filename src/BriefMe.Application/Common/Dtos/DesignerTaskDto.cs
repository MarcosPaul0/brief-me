using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of a task as its assigned Designer sees it on the board (BRF-033, RF017–RF020). Distinct from
/// <see cref="BriefingTaskDto"/>, which is the write use cases' echo of the aggregate: the board reads a work
/// plan, and a row that names only the task ("Banner topo") without the request it serves cannot be worked
/// from — so the parent briefing's title is resolved server-side and travels with the row.
/// </summary>
/// <remarks>
/// <see cref="BriefingTitle"/> is nullable because the lookup may not resolve — the briefing was removed, or
/// the caller's page outlived it. That degrades one cell; the row and its actions stay usable.
/// <see cref="Deadline"/> is nullable because planning is a separate act (RF018, BRF-028), and
/// <c>DesignerId</c> is absent on purpose: every row of this read model belongs to the caller.
/// </remarks>
public sealed record DesignerTaskDto(
    Guid Id,
    Guid BriefingId,
    string? BriefingTitle,
    string Title,
    DateOnly? Deadline,
    Priority Priority,
    BriefingTaskStatus Status)
{
    public static DesignerTaskDto FromBriefingTaskWithBriefingTitle(
        BriefingTask briefingTask,
        IReadOnlyDictionary<Guid, string> briefingTitles)
    {
        return new(
            briefingTask.Id,
            briefingTask.BriefingId,
            briefingTitles.GetValueOrDefault(briefingTask.BriefingId),
            briefingTask.Title,
            briefingTask.Deadline,
            briefingTask.Priority,
            briefingTask.Status);
    }
}
