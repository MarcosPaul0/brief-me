using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model behind the briefing tracking screen (BRF-039, RF023): the briefing's own header, how far it
/// has come, and the tasks it was broken into — the three things the screen shows, in one read.
/// </summary>
/// <remarks>
/// Distinct from <see cref="BriefingDto"/>, the write use cases' echo of the aggregate: that one is the
/// briefing alone, this one is the briefing plus everything a reader needs around it. The two must be free
/// to move apart.
/// <para>
/// <see cref="RequesterName"/> is nullable because the lookup may not resolve (a removed user degrades the
/// header's byline, not the page), and <see cref="DesiredDeliveryDate"/> because RF005 does not require it.
/// <see cref="Tasks"/> is empty — not null — for a briefing that has been approved but not decomposed yet
/// (BRF-026); the screen's empty state is what tells that story, matching <see cref="Progress"/>'s 0%.
/// </para>
/// </remarks>
public sealed record BriefingTrackingDto(
    Guid Id,
    string Title,
    string? Objective,
    Guid CategoryId,
    DateOnly? DesiredDeliveryDate,
    Priority Priority,
    BriefingStatus Status,
    Guid RequesterId,
    string? RequesterName,
    BriefingProgressDto Progress,
    IReadOnlyList<BriefingTrackingTaskDto> Tasks)
{
    /// <summary>
    /// Builds the tracking read from the briefing, the progress the domain service computed and the tasks
    /// the handler loaded. Pure, like every DTO factory: the percentage arrives already calculated
    /// (BRF-035) and the task order is the repository's — decomposition order, oldest first — and is not
    /// restated here.
    /// </summary>
    public static BriefingTrackingDto FromBriefingWithProgressAndTasks(
        Briefing briefing,
        BriefingProgress progress,
        IReadOnlyList<BriefingTask> tasks,
        IReadOnlyDictionary<Guid, string> personNames)
    {
        return new(
            briefing.Id,
            briefing.Title,
            briefing.Objective,
            briefing.CategoryId,
            briefing.DesiredDeliveryDate,
            briefing.Priority,
            briefing.Status,
            briefing.RequesterId,
            personNames.GetValueOrDefault(briefing.RequesterId),
            BriefingProgressDto.FromBriefingProgress(progress),
            [.. tasks.Select(task => BriefingTrackingTaskDto.FromBriefingTaskWithDesignerName(task, personNames))]);
    }
}
