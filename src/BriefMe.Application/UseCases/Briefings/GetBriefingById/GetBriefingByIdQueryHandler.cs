using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingById;

public sealed class GetBriefingByIdQueryHandler(
    IBriefingRepository briefingRepository,
    BriefingComplementsLoader complementsLoader,
    ICurrentUser currentUser) : IQueryHandler<GetBriefingByIdQuery, BriefingDetailDto>
{
    public async Task<Result<BriefingDetailDto>> Handle(GetBriefingByIdQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            return Result.Unauthorized<BriefingDetailDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDetailDto>(BriefingErrors.NotFound);

        // Three ways in: the requester's own briefing, the designer's assigned one, or — for any designer —
        // one still waiting in the queue, so they can read it before claiming.
        var isOwner = briefing.RequesterId == userId;
        var isAssignee = briefing.DesignerId == userId;
        var isQueuedForDesigner = briefing.Status is BriefingStatus.Submitted && currentUser.Role is UserRole.Designer;

        if (!isOwner && !isAssignee && !isQueuedForDesigner)
            return Result.Forbidden<BriefingDetailDto>(BriefingErrors.NotVisible);

        var complements = await complementsLoader.LoadAsync([briefing], cancellationToken);

        return Result.Success(BriefingDetailDto.FromBriefingWithComplements(briefing, complements));
    }
}
