using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model behind the task detail screen (BRF-034, RF021, RF022, RF052): the task's own planning data
/// plus the two trails it accumulates — its comments and its delivered files — with every person and the
/// parent briefing already named.
/// </summary>
/// <remarks>
/// Distinct from <see cref="BriefingTaskDto"/> on purpose. That one is the write use cases' echo of the
/// aggregate and knows only ids; this one is what a reader needs on screen, and the two must be free to
/// move apart. Distinct from <see cref="DesignerTaskDto"/> too: the board (BRF-033) is an index of rows and
/// carries no children.
/// <para>
/// <see cref="BriefingTitle"/> and <see cref="DesignerName"/> are nullable because the lookups may not
/// resolve — a removed briefing or a soft-deleted user degrades one line of the header, never the page.
/// <see cref="DesignerId"/> is nullable because assignment is a separate act (RF017, BRF-027), and
/// <see cref="Deadline"/> because planning is another (RF018, BRF-028).
/// </para>
/// </remarks>
public sealed record TaskDetailDto(
    Guid Id,
    Guid BriefingId,
    string? BriefingTitle,
    string Title,
    Guid? DesignerId,
    string? DesignerName,
    DateOnly? Deadline,
    Priority Priority,
    BriefingTaskStatus Status,
    IReadOnlyList<TaskCommentDto> Comments,
    IReadOnlyList<TaskDeliveryDto> Deliveries)
{
    /// <summary>
    /// Builds the detail from the task, its already-loaded children and the display names the handler
    /// resolved. Pure, like every DTO factory: ordering of the two trails is the repositories' (comments
    /// oldest first, deliveries newest first) and is not restated here.
    /// </summary>
    public static TaskDetailDto FromBriefingTaskWithComplements(
        BriefingTask briefingTask,
        IReadOnlyList<Attachment> deliveries,
        IReadOnlyDictionary<Guid, string> personNames,
        string? briefingTitle)
    {
        return new(
            briefingTask.Id,
            briefingTask.BriefingId,
            briefingTitle,
            briefingTask.Title,
            briefingTask.DesignerId,
            briefingTask.DesignerId is { } designerId ? personNames.GetValueOrDefault(designerId) : null,
            briefingTask.Deadline,
            briefingTask.Priority,
            briefingTask.Status,
            [.. briefingTask.Comments.Select(comment => TaskCommentDto.FromCommentWithAuthorName(comment, personNames))],
            [.. deliveries.Select(delivery => TaskDeliveryDto.FromAttachmentWithUploaderName(delivery, personNames))]);
    }
}
