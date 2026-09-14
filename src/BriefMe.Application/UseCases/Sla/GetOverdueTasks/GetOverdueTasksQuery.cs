using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Sla.GetOverdueTasks;

/// <summary>
/// Returns the tasks currently past their deadline, paginated (RF029, BRF-044) — the read behind the SLA
/// dashboard (BRF-046). The endpoint is <c>GET /tasks/overdue</c>.
/// </summary>
/// <remarks>
/// Lives under <c>UseCases/Sla</c> rather than <c>UseCases/BriefingTasks</c>: what it reads is the deadline
/// policy's outcome (RF027–RF029), written by the monitor of BRF-043, and its sibling is
/// <c>GetOverdueBriefingsQuery</c> (BRF-045) — not the task board.
/// <para>
/// Unscoped by design, unlike <c>GetTasksByDesignerQuery</c> (BRF-033): the delay list is read across every
/// designer by whoever answers for it, so the caller is not a filter and the <c>tasks.view-all</c> gate on
/// the route is what limits who gets here. It takes no filters either — RF029 asks which tasks are late,
/// and the repository decides that from the mark alone.
/// </para>
/// </remarks>
public sealed record GetOverdueTasksQuery(int Page, int PageSize) : IQuery<PagedDto<OverdueTaskDto>>;
