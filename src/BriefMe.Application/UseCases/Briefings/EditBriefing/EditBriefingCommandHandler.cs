using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.EditBriefing;

/// <summary>
/// Edits a briefing owned by the authenticated requester. A claimed or closed briefing yields an
/// <c>EditNotAllowed</c> conflict.
/// </summary>
public sealed class EditBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<EditBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(EditBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotOwnerToEdit);

        if (!briefing.IsOpenForRequester)
            return Result.Conflict<BriefingDto>(BriefingErrors.EditNotAllowed);

        briefing.Edit(
            request.Title,
            request.Priority,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
