using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingById;

/// <summary>
/// Loads a single briefing for the authenticated requester (BRF-013), with its attachments (RF005). Mirrors
/// the ownership guard of <c>EditBriefingCommandHandler</c>: unauthenticated → <c>Unauthorized</c>; unknown
/// id → <c>NotFound</c>; owned by another user → <c>Forbidden</c>. Never exposes the entity — returns
/// <see cref="BriefingDetailDto"/>.
/// </summary>
public sealed class GetBriefingByIdQueryHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser) : IQueryHandler<GetBriefingByIdQuery, BriefingDetailDto>
{
    public async Task<Result<BriefingDetailDto>> Handle(GetBriefingByIdQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDetailDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdWithAttachmentsAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDetailDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDetailDto>(BriefingErrors.NotOwnerToView);

        return Result.Success(BriefingDetailDto.FromBriefingWithAttachments(briefing));
    }
}
