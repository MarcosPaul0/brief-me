using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;

/// <summary>
/// Transitions a briefing to <c>ChangesRequested</c> (RF014, BRF-018). Loads the aggregate and delegates the
/// transition to <c>Briefing.RequestChanges()</c>, which records the reviewer plus the justification and
/// raises <c>ChangesRequested</c> (consumed later by Notifications — BRF-052, RF034).
/// </summary>
/// <remarks>
/// The briefing lands back in the requester's hands: <c>ChangesRequested</c> is editable
/// (<c>EditBriefingCommandHandler</c>) and submittable (<c>SubmitBriefingForApprovalCommandHandler</c>), which
/// closes the RF014 cycle. As in BRF-016/BRF-017 there is <b>no ownership guard</b> — the authenticated user
/// is the <i>reviewer</i>, and whether they may review at all is decided by the <c>briefings.approve</c>
/// policy on the endpoint (BRF-019).
/// </remarks>
public sealed class RequestChangesBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestChangesBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(RequestChangesBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requestedById))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        // Re-checked here even though the validator already ran: the handler must stand on its own, and
        // Briefing.RequestChanges() answers a missing reason with a DomainException — an exception is for a
        // programming error, not for user input (RF014; CLAUDE.md — Result over exceptions).
        var justification = Justification.Create(request.Justification);
        if (justification.IsFailure)
            return Result.Failure<BriefingDto>(justification.Error!);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        // Reviewing a briefing that already left AwaitingApproval is an expected outcome (a stale panel row,
        // two open tabs, changes requested twice in a row), not a bug: return a conflict instead of letting
        // Briefing.RequestChanges() throw and surface as a 500 (RF014).
        if (briefing.Status is not BriefingStatus.AwaitingApproval)
            return Result.Conflict<BriefingDto>(BriefingErrors.ChangeRequestNotAllowed(briefing.Status));

        briefing.RequestChanges(justification.Value.Value, requestedById);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
