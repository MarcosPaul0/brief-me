using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.SubmitBriefing;

/// <summary>
/// Sends the requester's draft to the designer queue. The completeness rules live in
/// <c>Briefing.Submit()</c>, which answers missing fields as a validation failure.
/// </summary>
public sealed class SubmitBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<SubmitBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(SubmitBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotOwnerToSubmit);

        if (briefing.Status is not BriefingStatus.Draft)
            return Result.Conflict<BriefingDto>(BriefingErrors.NotSubmittable);

        var submission = briefing.Submit();
        if (submission.IsFailure)
            return Result.Failure<BriefingDto>(submission.Errors);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
