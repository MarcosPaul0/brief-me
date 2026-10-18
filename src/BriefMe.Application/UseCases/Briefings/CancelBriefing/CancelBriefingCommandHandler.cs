using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.CancelBriefing;

public sealed class CancelBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(CancelBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);
        
        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotOwnerToSubmit);

        // Cancelling a claimed or already cancelled briefing is an expected outcome (a stale list row, a
        // second tab), not a bug: return a conflict instead of letting Briefing.Cancel() throw.
        if (briefing.Status is BriefingStatus.Cancelled)
            return Result.Conflict<BriefingDto>(BriefingErrors.AlreadyCancelled);

        if (!briefing.IsOpenForRequester)
            return Result.Conflict<BriefingDto>(BriefingErrors.CancellationNotAllowed(briefing.Status));

        briefing.Cancel();

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}