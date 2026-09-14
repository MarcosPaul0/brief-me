using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;

/// <summary>
/// Plans an existing task: its delivery deadline (RF018) and its priority (RF019), BRF-028. The two travel
/// together because they are one act of the Design Manager — the endpoint behind it is a single
/// <c>PUT /tasks/{id}/deadline-priority</c> (BRF-032).
/// </summary>
/// <remarks>
/// <see cref="Deadline"/> is not nullable: RF018 is "define <b>a</b> deadline for each task", and having no
/// deadline is the state a task is created in (BRF-026), not something this command produces. The caller is
/// the authenticated user (<c>ICurrentUser</c>) — a Design Manager, enforced by the policy on the endpoint —
/// and is not part of the command.
/// </remarks>
public sealed record SetDeadlinePriorityCommand(
    Guid TaskId,
    DateOnly Deadline,
    Priority Priority) : ICommand<BriefingTaskDto>;
