using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;

/// <summary>
/// Reads the review queue for the Design Manager's approval panel (BRF-020, RF011–RF014): the briefings in
/// <c>AwaitingApproval</c>, oldest first, enriched with the requester and category names.
/// </summary>
/// <remarks>
/// The names are resolved here, not in the browser: <c>GET /categories</c> is gated on
/// <c>briefings.create</c> (BRF-013), a permission the Design Manager does not hold, so a client-side lookup
/// would show every row as "—". Both lookups are batched — one <c>GetByIdsAsync</c> for the page's
/// requesters and one read of the category catalog — so the queue costs a constant number of queries
/// regardless of page size. An unresolvable name stays <c>null</c> and degrades that cell alone.
/// </remarks>
public sealed class GetBriefingsPendingApprovalQueryHandler(
    IBriefingRepository briefingRepository,
    IUserRepository userRepository,
    ICategoryRepository categoryRepository) : IQueryHandler<GetBriefingsPendingApprovalQuery, PagedDto<PendingApprovalBriefingDto>>
{
    public async Task<Result<PagedDto<PendingApprovalBriefingDto>>> Handle(
        GetBriefingsPendingApprovalQuery request,
        CancellationToken cancellationToken)
    {
        var briefings = await briefingRepository.GetPendingApprovalAsync(request.Page, request.PageSize, cancellationToken);
        var totalCount = await briefingRepository.CountPendingApprovalAsync(cancellationToken);

        var requesterNames = await GetRequesterNamesAsync(briefings, cancellationToken);
        var categoryNames = await GetCategoryNamesAsync(cancellationToken);

        var items = briefings
            .Select(briefing => PendingApprovalBriefingDto.FromBriefingWithComplements(briefing, requesterNames, categoryNames))
            .ToList();

        return Result.Success(PagedDto<PendingApprovalBriefingDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }

    private async Task<Dictionary<Guid, string>> GetRequesterNamesAsync(
        IReadOnlyList<Briefing> briefings,
        CancellationToken cancellationToken)
    {
        var requesterIds = briefings.Select(briefing => briefing.RequesterId).Distinct().ToList();

        var users = await userRepository.GetByIdsAsync(requesterIds, cancellationToken);

        return users.ToDictionary(user => user.Id, user => user.FullName);
    }

    private async Task<Dictionary<Guid, string>> GetCategoryNamesAsync(CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetActiveAsync(cancellationToken);

        return categories.ToDictionary(category => category.Id, category => category.Name);
    }
}
