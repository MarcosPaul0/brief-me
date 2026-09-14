using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;

/// <summary>
/// Returns the briefings awaiting review, paginated, for the Design Manager's approval panel (BRF-020,
/// RF011–RF014). Takes no scope parameter: the queue is the same for every reviewer, and the right to see
/// it is decided by the <c>briefings.view-all</c> policy on the endpoint (BRF-019's model — <c>ICurrentUser</c>
/// exposes no permissions). No status filter either — <c>AwaitingApproval</c> is what makes it a queue.
/// </summary>
public sealed record GetBriefingsPendingApprovalQuery(
    int Page,
    int PageSize) : IQuery<PagedDto<PendingApprovalBriefingDto>>;
