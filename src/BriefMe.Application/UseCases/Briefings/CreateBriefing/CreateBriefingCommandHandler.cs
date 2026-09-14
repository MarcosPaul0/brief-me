using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.CreateBriefing;

/// <summary>
/// Creates a briefing in <c>Draft</c> for the authenticated requester and persists it (RF004, RF006).
/// Validates that the given category exists and is active (RF040) before touching the aggregate.
/// </summary>
public sealed class CreateBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICategoryRepository categoryRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(CreateBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        if (!await categoryRepository.ExistsActiveAsync(request.CategoryId, cancellationToken))
            return Result.NotFound<BriefingDto>(CategoryErrors.NotFoundOrInactive);

        var briefing = Briefing.Create(
            request.Title,
            request.Priority,
            request.CategoryId,
            requesterId,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate);

        await briefingRepository.AddAsync(briefing, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
