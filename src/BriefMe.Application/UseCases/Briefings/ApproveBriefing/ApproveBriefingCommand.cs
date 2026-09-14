using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.ApproveBriefing;

/// <summary>
/// Approves a briefing under review (RF011, BRF-016), moving it from <c>AwaitingApproval</c> to
/// <c>Approved</c>. The reviewer is the authenticated user (<c>ICurrentUser</c>) — a Design Manager, enforced
/// by the <c>briefings.approve</c> policy on the endpoint (BRF-019) — so only the target
/// <see cref="BriefingId"/> is carried.
/// </summary>
public sealed record ApproveBriefingCommand(Guid BriefingId) : ICommand<BriefingDto>;
