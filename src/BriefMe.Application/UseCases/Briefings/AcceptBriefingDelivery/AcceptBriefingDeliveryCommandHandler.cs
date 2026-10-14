using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;

public sealed class AcceptBriefingDeliveryCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<AcceptBriefingDeliveryCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(AcceptBriefingDeliveryCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotOwnerToReview);

        if (briefing.Status is not BriefingStatus.Delivered)
            return Result.Conflict<BriefingDto>(BriefingErrors.AcceptNotAllowed(briefing.Status));

        briefing.Accept();

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
