using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.ApproveBriefing;

/// <summary>
/// Transitions a briefing to <c>Approved</c> (RF011, BRF-016). Loads the aggregate and delegates the
/// transition to <c>Briefing.Approve()</c>, which records the reviewer and raises <c>BriefingApproved</c>
/// (consumed later by Notifications — BRF-050).
/// </summary>
/// <remarks>
/// Unlike the requester-facing handlers, there is <b>no ownership guard</b>: approving is by definition an act
/// of a third party (the Design Manager) over someone else's briefing, so the authenticated user is the
/// <i>reviewer</i>, not the owner. Whether that user may approve at all is decided by the
/// <c>briefings.approve</c> policy on the endpoint (BRF-019) — <c>ICurrentUser</c> exposes no permissions.
/// </remarks>
public sealed class ApproveBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<ApproveBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(ApproveBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var approvedById))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        // Reviewing a briefing that already left AwaitingApproval is an expected outcome (a stale panel row,
        // two open tabs), not a bug: return a conflict instead of letting Briefing.Approve() throw and surface
        // as a 500 (CLAUDE.md — Result over exceptions for expected failures; RF011).
        if (briefing.Status is not BriefingStatus.AwaitingApproval)
            return Result.Conflict<BriefingDto>(BriefingErrors.ApprovalNotAllowed(briefing.Status));

        briefing.Approve(approvedById);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
