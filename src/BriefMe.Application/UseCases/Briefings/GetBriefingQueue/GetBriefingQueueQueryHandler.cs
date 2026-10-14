using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingQueue;

public sealed class GetBriefingQueueQueryHandler(
    IBriefingRepository briefingRepository,
    BriefingComplementsLoader complementsLoader) : IQueryHandler<GetBriefingQueueQuery, PagedDto<BriefingListItemDto>>
{
    public async Task<Result<PagedDto<BriefingListItemDto>>> Handle(GetBriefingQueueQuery request, CancellationToken cancellationToken)
    {
        var briefings = await briefingRepository.GetQueueAsync(request.Page, request.PageSize, cancellationToken);
        var totalCount = await briefingRepository.CountQueueAsync(cancellationToken);

        var complements = await complementsLoader.LoadAsync(briefings, cancellationToken);
        var items = briefings
            .Select(briefing => BriefingListItemDto.FromBriefingWithComplements(briefing, complements))
            .ToList();

        return Result.Success(PagedDto<BriefingListItemDto>.Create(items, request.Page, request.PageSize, totalCount));
    }
}
