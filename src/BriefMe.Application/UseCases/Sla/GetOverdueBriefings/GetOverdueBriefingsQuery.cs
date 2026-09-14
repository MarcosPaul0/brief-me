using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Sla.GetOverdueBriefings;

/// <summary>
/// Returns the briefings currently past their due instant, paginated (RF030, BRF-045) — the demand half of
/// the SLA dashboard (BRF-046), next to <c>GetOverdueTasksQuery</c>'s task half. The endpoint is
/// <c>GET /briefings/overdue</c>.
/// </summary>
/// <remarks>
/// Lives under <c>UseCases/Sla</c> rather than <c>UseCases/Briefings</c>: what it reads is the deadline
/// policy's outcome (RF027–RF030), written by the monitor of BRF-043, and its sibling is
/// <c>GetOverdueTasksQuery</c> (BRF-044) — not the requester's list.
/// <para>
/// Unscoped by design, unlike <c>GetBriefingsByRequesterQuery</c> (BRF-008): the delay list is read across
/// every requester by whoever answers for it, so the caller is not a filter and the
/// <c>briefings.view-all</c> gate on the route is what limits who gets here. It takes no filters either —
/// RF030 asks which briefings are late, and the repository decides that from the mark alone.
/// </para>
/// </remarks>
public sealed record GetOverdueBriefingsQuery(int Page, int PageSize) : IQuery<PagedDto<OverdueBriefingDto>>;
