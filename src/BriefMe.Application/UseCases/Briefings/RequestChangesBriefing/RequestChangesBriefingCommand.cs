using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;

/// <summary>
/// Returns a briefing under review to its requester for corrections (RF014, BRF-018), moving it from
/// <c>AwaitingApproval</c> to <c>ChangesRequested</c>. The reviewer is the authenticated user
/// (<c>ICurrentUser</c>) — a Design Manager, enforced by the <c>briefings.approve</c> policy on the endpoint
/// (BRF-019) — so only the target <see cref="BriefingId"/> and the mandatory <see cref="Justification"/> are
/// carried. Unlike a rejection (BRF-017), the briefing stays alive and editable.
/// </summary>
public sealed record RequestChangesBriefingCommand(Guid BriefingId, string Justification) : ICommand<BriefingDto>;
