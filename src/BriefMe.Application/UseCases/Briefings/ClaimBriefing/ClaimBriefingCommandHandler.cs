using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.ClaimBriefing;

public sealed class ClaimBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<ClaimBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(ClaimBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var designerId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        // Claimed or cancelled between listing and clicking: an expected race, answered as a conflict.
        if (briefing.Status is not BriefingStatus.Submitted)
            return Result.Conflict<BriefingDto>(BriefingErrors.ClaimNotAllowed(briefing.Status));

        briefing.Claim(designerId);
        briefingRepository.Update(briefing);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another designer committed the same claim between our read and our write.
            return Result.Conflict<BriefingDto>(BriefingErrors.AlreadyClaimed);
        }

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
