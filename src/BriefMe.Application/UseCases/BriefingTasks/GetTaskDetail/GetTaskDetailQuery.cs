using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.BriefingTasks.GetTaskDetail;

/// <summary>
/// Reads one task with its comment trail and its delivered files (RF021, RF022, RF052, BRF-034). The
/// endpoint behind it is <c>GET /tasks/{id}</c> and the screen is the task detail.
/// </summary>
/// <remarks>
/// The caller is not a parameter: who may read this particular task is decided server-side from
/// <c>ICurrentUser</c> against the task's assignee and the parent briefing's requester — the same rule as
/// <c>RegisterCommentCommand</c> (BRF-030), and the same reason <c>GetTasksByDesignerQuery</c> takes no
/// designer id (BRF-033).
/// </remarks>
public sealed record GetTaskDetailQuery(Guid TaskId) : IQuery<TaskDetailDto>;
