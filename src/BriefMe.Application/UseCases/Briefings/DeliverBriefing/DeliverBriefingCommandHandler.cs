using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.DeliverBriefing;

public sealed class DeliverBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<DeliverBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(DeliverBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var designerId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        // Re-checked although the validator already ran: Briefing.Deliver() answers a bad note with a
        // DomainException, which is for programming errors, not user input.
        if (string.IsNullOrWhiteSpace(request.DeliveryNote))
            return Result.Failure<BriefingDto>(BriefingErrors.Validation.DeliveryNoteRequired);

        if (request.DeliveryNote.Trim().Length > Briefing.DeliveryNoteMaxLength)
            return Result.Failure<BriefingDto>(BriefingErrors.Validation.DeliveryNoteTooLong(Briefing.DeliveryNoteMaxLength));

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.DesignerId != designerId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotAssignee);

        if (briefing.Status is not BriefingStatus.InProgress)
            return Result.Conflict<BriefingDto>(BriefingErrors.DeliveryNotAllowed(briefing.Status));

        briefing.Deliver(designerId, request.DeliveryNote);

        briefingRepository.Update(briefing);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
