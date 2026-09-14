using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;

/// <summary>
/// Reads the authenticated requester's briefings (RF008, BRF-008). Scopes the result to
/// <c>ICurrentUser.UserId</c> and delegates filtering/pagination to <c>IBriefingRepository</c>. A
/// non-authenticated context yields <c>Unauthorized</c> before touching the repository.
/// </summary>
public sealed class GetBriefingsByRequesterQueryHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser) : IQueryHandler<GetBriefingsByRequesterQuery, PagedDto<BriefingDto>>
{
    public async Task<Result<PagedDto<BriefingDto>>> Handle(GetBriefingsByRequesterQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<PagedDto<BriefingDto>>(CommonErrors.UserNotAuthenticated);

        var filter = new BriefingFilter(request.Status, request.CategoryId);

        var briefings = await briefingRepository.GetByRequesterAsync(
            requesterId,
            filter,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await briefingRepository.CountByRequesterAsync(requesterId, filter, cancellationToken);

        var items = briefings.Select(BriefingDto.FromBriefing).ToList();

        return Result.Success(PagedDto<BriefingDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }
}
