using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;

/// <summary>
/// Transitions a briefing to <c>AwaitingApproval</c> (RF007, BRF-007). Loads the aggregate, checks that the
/// authenticated user owns it, then delegates the transition — including RF005 completeness — to
/// <c>Briefing.SubmitForApproval()</c>, which raises <c>BriefingSubmittedForApproval</c> on success.
/// Category-checklist validation (RF063) is deferred to BRF-093; when <c>IChecklistValidator</c> exists it is
/// applied here before persisting the transition.
/// </summary>
public sealed class SubmitBriefingForApprovalCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<SubmitBriefingForApprovalCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(SubmitBriefingForApprovalCommand request, CancellationToken cancellationToken)
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
