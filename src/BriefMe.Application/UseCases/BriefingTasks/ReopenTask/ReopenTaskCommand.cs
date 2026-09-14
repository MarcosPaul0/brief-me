using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.BriefingTasks.ReopenTask;

/// <summary>
/// Reopens a task the requester had already approved (RF025, BRF-037), sending it back to execution and —
/// when it was the briefing's last pending work — bringing the briefing back with it (RF058).
/// </summary>
/// <remarks>
/// The task id is the whole command. No justification: RF025 is a single sentence with no qualifier, unlike
/// the rejections of RF013/RF044/RF047, which name one explicitly. The caller is the authenticated user
/// (<c>ICurrentUser</c>) — a Design Manager, enforced by the <c>tasks.reopen</c> policy on the endpoint — and
/// is not part of the command.
/// </remarks>
public sealed record ReopenTaskCommand(Guid TaskId) : ICommand<BriefingTaskDto>;
