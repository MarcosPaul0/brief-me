using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.RejectBriefing;

/// <summary>
/// Rejects a briefing under review (RF012, BRF-017), moving it from <c>AwaitingApproval</c> to
/// <c>Rejected</c>. The reviewer is the authenticated user (<c>ICurrentUser</c>) — a Design Manager, enforced
/// by the <c>briefings.approve</c> policy on the endpoint (BRF-019) — so only the target
/// <see cref="BriefingId"/> and the mandatory <see cref="Justification"/> (RF013) are carried.
/// </summary>
public sealed record RejectBriefingCommand(Guid BriefingId, string Justification) : ICommand<BriefingDto>;
