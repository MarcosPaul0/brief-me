using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

public interface IBriefingRepository : IRepository<Briefing>
{
    /// <summary>
    /// The briefing with its <see cref="Briefing.Attachments"/> loaded, or null. Use it wherever the collection is
    /// read or changed; <c>GetByIdAsync</c> leaves it empty so the other transitions never pay the join.
    /// </summary>
    Task<Briefing?> GetByIdWithAttachmentsAsync(Guid id, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// One page of the open queue: submitted briefings nobody has claimed yet, oldest first, so designers
    /// pick up whatever has waited longest. The total comes from <see cref="CountQueueAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Briefing>> GetQueueAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the briefings claimed by <paramref name="designerId"/>, optionally filtered by status,
    /// ordered by desired delivery date (undated last) then creation. The total comes from
    /// <see cref="CountByDesignerAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Briefing>> GetByDesignerAsync(
        Guid designerId,
        BriefingStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountByDesignerAsync(Guid designerId, BriefingStatus? status, CancellationToken cancellationToken = default);
}
