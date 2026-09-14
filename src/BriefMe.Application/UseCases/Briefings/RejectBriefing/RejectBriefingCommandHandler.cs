using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Briefings.RejectBriefing;

/// <summary>
/// Transitions a briefing to <c>Rejected</c> (RF012, BRF-017). Loads the aggregate and delegates the
/// transition to <c>Briefing.Reject()</c>, which records the reviewer plus the justification and raises
/// <c>BriefingRejected</c> (consumed later by Notifications — BRF-050, RF032).
/// </summary>
/// <remarks>
/// As in <c>ApproveBriefingCommandHandler</c>, there is <b>no ownership guard</b>: rejecting is by definition
/// an act of a third party (the Design Manager) over someone else's briefing, so the authenticated user is the
/// <i>reviewer</i>, not the owner. Whether that user may reject at all is decided by the
/// <c>briefings.approve</c> policy on the endpoint (BRF-019) — <c>ICurrentUser</c> exposes no permissions.
/// </remarks>
public sealed class RejectBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<RejectBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(RejectBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var rejectedById))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        // Re-checked here even though the validator already ran: the handler must stand on its own, and
        // Briefing.Reject() answers a missing reason with a DomainException — an exception is for a
        // programming error, not for user input (RF013; CLAUDE.md — Result over exceptions).
        var justification = Justification.Create(request.Justification);
        if (justification.IsFailure)
            return Result.Failure<BriefingDto>(justification.Error!);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        // Reviewing a briefing that already left AwaitingApproval is an expected outcome (a stale panel row,
        // two open tabs), not a bug: return a conflict instead of letting Briefing.Reject() throw and surface
        // as a 500 (RF012).
        if (briefing.Status is not BriefingStatus.AwaitingApproval)
            return Result.Conflict<BriefingDto>(BriefingErrors.RejectionNotAllowed(briefing.Status));

        briefing.Reject(justification.Value.Value, rejectedById);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
