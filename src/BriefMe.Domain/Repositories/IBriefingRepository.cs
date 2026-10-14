using BriefMe.Domain.Entities;

namespace BriefMe.Domain.Repositories;

public interface IBriefingRepository : IRepository<Briefing>
{
    /// <summary>
    /// Returns one page of a requester's briefings, filtered, ordered from newest to oldest.
    /// The total behind the filter comes from <see cref="CountByRequesterAsync"/> — the caller assembles the
    /// two into its own paging envelope.
    /// <c>GetByIdAsync</c> and <c>AddAsync</c> come from <see cref="IRepository{T}"/>.
    /// </summary>
    Task<IReadOnlyList<Briefing>> GetByRequesterAsync(
        Guid requesterId,
        BriefingFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the requester's briefings matching <paramref name="filter"/>, ignoring pagination. Same filter
    /// the caller passed to <see cref="GetByRequesterAsync"/>.
    /// </summary>
    Task<int> CountByRequesterAsync(
        Guid requesterId,
        BriefingFilter filter,
        CancellationToken cancellationToken = default);
}
