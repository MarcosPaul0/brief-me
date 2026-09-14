using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.BriefingTasks.AssignTask;

/// <summary>
/// Assigns a task to a Designer (RF017, BRF-027). Carries a single task because RF017 is "assign <b>a</b>
/// task to a Designer" — unlike RF015, the requirement has no plural.
/// </summary>
/// <remarks>
/// The assigner is the authenticated user (<c>ICurrentUser</c>) — a Design Manager, enforced by the
/// <c>tasks.assign</c> policy on the endpoint (BRF-032) — and is not part of the command. Handing an already
/// assigned task to someone else is reassignment (RF026, BRF-038), a different command.
/// </remarks>
public sealed record AssignTaskCommand(Guid TaskId, Guid DesignerId) : ICommand<BriefingTaskDto>;
