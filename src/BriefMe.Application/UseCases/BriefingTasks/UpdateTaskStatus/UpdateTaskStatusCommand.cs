using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;

/// <summary>
/// Moves an existing task through the execution states of RF020 (BRF-029). The endpoint behind it is
/// <c>PUT /tasks/{id}/status</c> (BRF-032).
/// </summary>
/// <remarks>
/// <see cref="Status"/> accepts the whole <see cref="BriefingTaskStatus"/> enum, but the handler only opens
/// the execution doors — <c>NotStarted</c>, <c>InProgress</c> and <c>Blocked</c>. The four approval states
/// (RF042–RF048) are reached through their own use cases (BRF-066/067/069), which carry rules this command
/// cannot apply. The caller is the authenticated user (<c>ICurrentUser</c>) and is not part of the command:
/// RF020 belongs to the task's assignee, which the handler reads from the aggregate.
/// </remarks>
public sealed record UpdateTaskStatusCommand(
    Guid TaskId,
    BriefingTaskStatus Status) : ICommand<BriefingTaskDto>;
