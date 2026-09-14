using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.BriefingTasks.GetTasksByDesigner;

/// <summary>
/// Returns the authenticated Designer's own tasks, paginated, with optional filters by
/// <see cref="Status"/> and <see cref="Priority"/> (RF017–RF020, BRF-033). The endpoint behind it is
/// <c>GET /tasks</c> (BRF-033), and the screen is the task board.
/// </summary>
/// <remarks>
/// The designer is taken from <c>ICurrentUser</c> and is deliberately not a parameter — the same rule as
/// <c>GetBriefingsByRequesterQuery</c> (BRF-008): who may see whose work is decided server-side, so no
/// caller can widen the scope by sending someone else's id. A Design Manager's view of every task
/// (<c>tasks.view-all</c>) is a different read and is not this query.
/// </remarks>
public sealed record GetTasksByDesignerQuery(
    int Page,
    int PageSize,
    BriefingTaskStatus? Status = null,
    Priority? Priority = null) : IQuery<PagedDto<DesignerTaskDto>>;
