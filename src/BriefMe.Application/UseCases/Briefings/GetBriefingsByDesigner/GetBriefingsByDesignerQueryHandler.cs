using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByDesigner;

public sealed class GetBriefingsByDesignerQueryHandler(
    IBriefingRepository briefingRepository,
    BriefingComplementsLoader complementsLoader,
    ICurrentUser currentUser) : IQueryHandler<GetBriefingsByDesignerQuery, PagedDto<BriefingListItemDto>>
{
    public async Task<Result<PagedDto<BriefingListItemDto>>> Handle(GetBriefingsByDesignerQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var designerId))
            return Result.Unauthorized<PagedDto<BriefingListItemDto>>(CommonErrors.UserNotAuthenticated);

        var briefings = await briefingRepository.GetByDesignerAsync(
            designerId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);
        var totalCount = await briefingRepository.CountByDesignerAsync(designerId, request.Status, cancellationToken);

        var complements = await complementsLoader.LoadAsync(briefings, cancellationToken);
        var items = briefings
            .Select(briefing => BriefingListItemDto.FromBriefingWithComplements(briefing, complements))
            .ToList();

        return Result.Success(PagedDto<BriefingListItemDto>.Create(items, request.Page, request.PageSize, totalCount));
    }
}
