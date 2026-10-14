using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;

public sealed class RequestBriefingChangesCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestBriefingChangesCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(RequestBriefingChangesCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        // Re-checked although the validator already ran: Briefing.RequestChanges() answers a bad reason with a
        // DomainException, which is for programming errors, not user input.
        var reason = Justification.Create(request.Reason);
        if (reason.IsFailure)
            return Result.Failure<BriefingDto>(reason.Error!);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotOwnerToReview);

        if (briefing.Status is not BriefingStatus.Delivered)
            return Result.Conflict<BriefingDto>(BriefingErrors.ChangeRequestNotAllowed(briefing.Status));

        briefing.RequestChanges(reason.Value.Value);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
