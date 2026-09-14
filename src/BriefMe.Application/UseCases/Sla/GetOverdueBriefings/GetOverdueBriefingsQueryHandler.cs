using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Sla.GetOverdueBriefings;

/// <summary>
/// Reads the overdue briefing list (RF030, BRF-045): one page of the demands the deadline monitor marked and
/// that are still open, longest late first, each carrying the delivery date it broke, its requester and its
/// category.
/// </summary>
/// <remarks>
/// Lateness is not recomputed here. The monitor (BRF-043) resolved the applicable SLA rule (RF027), decided
/// the effective due instant and stored the mark; this query reports it. Deciding it a second time would
/// need the whole policy loaded per request and could contradict the <c>DeadlineOverdue</c> already
/// published (RF035) if a rule changed in between.
/// <para>
/// Both display names are resolved server-side for the same reason the approval panel resolves its own
/// (BRF-020): the Design Manager reading this list holds neither <c>users.view</c> nor <c>briefings.create</c>,
/// the permissions behind the user and category routes. The requesters come in a single batched
/// <c>GetByIdsAsync</c> and the categories in one read of the catalog, so the page costs a constant number of
/// queries whatever its size, and an unresolved name degrades one cell rather than the whole list.
/// </para>
/// </remarks>
public sealed class GetOverdueBriefingsQueryHandler(
    IBriefingRepository briefingRepository,
    IUserRepository userRepository,
    ICategoryRepository categoryRepository) : IQueryHandler<GetOverdueBriefingsQuery, PagedDto<OverdueBriefingDto>>
{
    public async Task<Result<PagedDto<OverdueBriefingDto>>> Handle(
        GetOverdueBriefingsQuery request,
        CancellationToken cancellationToken)
    {
        var briefings = await briefingRepository.GetOverdueAsync(request.Page, request.PageSize, cancellationToken);
        var totalCount = await briefingRepository.CountOverdueAsync(cancellationToken);

        var requesterNames = await GetRequesterNamesAsync(briefings, cancellationToken);
        var categoryNames = await GetCategoryNamesAsync(cancellationToken);

        var items = briefings
            .Select(briefing => OverdueBriefingDto.FromBriefingWithComplements(briefing, requesterNames, categoryNames))
            .ToList();

        return Result.Success(PagedDto<OverdueBriefingDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }

    /// <summary>
    /// Names of the requesters behind this page, keyed by id. Distinct ids only: a requester with five late
    /// briefings is read once.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetRequesterNamesAsync(
        IReadOnlyList<Briefing> briefings,
        CancellationToken cancellationToken)
    {
        var requesterIds = briefings.Select(briefing => briefing.RequesterId).Distinct().ToList();

        var users = await userRepository.GetByIdsAsync(requesterIds, cancellationToken);

        return users.ToDictionary(user => user.Id, user => user.FullName);
    }

    /// <summary>
    /// The active category catalog, keyed by id — the same lookup the approval panel uses (BRF-020). A
    /// briefing whose category was deactivated resolves to <c>null</c> and keeps its row: the demand is late
    /// either way.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetCategoryNamesAsync(CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetActiveAsync(cancellationToken);

        return categories.ToDictionary(category => category.Id, category => category.Name);
    }
}
