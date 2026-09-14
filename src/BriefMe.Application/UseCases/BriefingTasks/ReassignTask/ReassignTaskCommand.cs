using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.BriefingTasks.ReassignTask;

/// <summary>
/// Transfers a task to a different Designer (RF026, BRF-038). Carries the new assignee only — the one being
/// replaced is whoever the task currently holds, so naming them in the command would only allow the caller
/// to disagree with the database.
/// </summary>
/// <remarks>
/// The caller is the authenticated user (<c>ICurrentUser</c>) — a Design Manager, enforced by the
/// <c>tasks.reassign</c> policy on the endpoint (BRF-032) — and is not part of the command. Giving a task its
/// <b>first</b> assignee is RF017 (BRF-027), a different command with a different permission.
/// </remarks>
public sealed record ReassignTaskCommand(Guid TaskId, Guid DesignerId) : ICommand<BriefingTaskDto>;
