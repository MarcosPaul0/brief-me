using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;

public sealed class GetBriefingsByRequesterQueryHandler(
    IBriefingRepository briefingRepository,
    BriefingComplementsLoader complementsLoader,
    ICurrentUser currentUser) : IQueryHandler<GetBriefingsByRequesterQuery, PagedDto<BriefingListItemDto>>
{
    public async Task<Result<PagedDto<BriefingListItemDto>>> Handle(GetBriefingsByRequesterQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<PagedDto<BriefingListItemDto>>(CommonErrors.UserNotAuthenticated);

        var filter = new BriefingFilter(request.Status, request.CategoryId);

        var briefings = await briefingRepository.GetByRequesterAsync(
            requesterId,
            filter,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await briefingRepository.CountByRequesterAsync(requesterId, filter, cancellationToken);

        var complements = await complementsLoader.LoadAsync(briefings, cancellationToken);
        var items = briefings
            .Select(briefing => BriefingListItemDto.FromBriefingWithComplements(briefing, complements))
            .ToList();

        return Result.Success(PagedDto<BriefingListItemDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }
}
