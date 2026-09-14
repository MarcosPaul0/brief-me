using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Output representation of a task for outer layers (BRF-026). Never exposes the domain entity directly.
/// <c>DesignerId</c> and <c>Deadline</c> are nullable because a freshly created task has neither: assignment
/// is RF017 (BRF-027) and the deadline is RF018 (BRF-028).
/// </summary>
public sealed record BriefingTaskDto(
    Guid Id,
    Guid BriefingId,
    string Title,
    Guid? DesignerId,
    DateOnly? Deadline,
    Priority Priority,
    BriefingTaskStatus Status)
{
    public static BriefingTaskDto FromBriefingTask(BriefingTask briefingTask)
    {
        return new(
            briefingTask.Id,
            briefingTask.BriefingId,
            briefingTask.Title,
            briefingTask.DesignerId,
            briefingTask.Deadline,
            briefingTask.Priority,
            briefingTask.Status);
    }
}
