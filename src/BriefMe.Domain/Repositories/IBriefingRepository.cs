using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

public interface IBriefingRepository : IRepository<Briefing>
{
    /// <summary>
    /// Returns one page of a requester's briefings, filtered, ordered from newest to oldest (BRF-005).
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
    /// Returns the briefings whose ids are in <paramref name="ids"/>, in no guaranteed order and without the
    /// ones that do not exist. Batches the display-name lookup a read model needs when it shows tasks or rows
    /// from several briefings at once (BRF-033), so the caller pays one query instead of one per row. Mirrors
    /// <see cref="IUserRepository.GetByIdsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Briefing>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a briefing together with its own attachments (RF005), newest first — the read the detail screen
    /// and the removal use case need. Task deliveries are excluded: they hang off the same aggregate but
    /// belong to the task screen (BRF-034), and a briefing under execution can hold hundreds of them.
    /// </summary>
    /// <remarks>
    /// A separate method rather than an <c>Include</c> on <c>GetByIdAsync</c>: every write path
    /// (submit, approve, cancel, …) loads the root alone and must not pay a join for a collection it never
    /// reads. Conversely, removing an attachment <b>must</b> use this one — <c>Briefing.RemoveAttachment</c>
    /// filters an in-memory list, so against an unloaded collection it is a silent no-op.
    /// </remarks>
    Task<Briefing?> GetByIdWithAttachmentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the files delivered on one task (RF022, RF052), newest first — the current delivery leads and
    /// the earlier ones read as its history (the numbered version list is RF049, BRF-076). Briefing
    /// attachments (RF005) are excluded: only rows carrying the task are deliveries.
    /// </summary>
    /// <remarks>
    /// Lives on this repository, not on <see cref="IBriefingTaskRepository"/> and not on one of its own,
    /// because <see cref="Attachment"/> is a child of the <see cref="Briefing"/> aggregate (BRF-004):
    /// reading a child goes through the repository of the aggregate that owns it, exactly as writing one
    /// goes through the root (<c>Briefing.AddDelivery</c>).
    /// </remarks>
    Task<IReadOnlyList<Attachment>> GetDeliveriesByTaskAsync(
        Guid taskId,
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
    /// Returns one page of the briefings waiting for the Design Manager's review — status
    /// <c>AwaitingApproval</c> — ordered oldest first (BRF-020, RF011–RF014). Not scoped to a
    /// requester: reviewing is an act over someone else's briefing, so the caller's permission
    /// (<c>briefings.view-all</c>) is the only gate. FIFO order is deliberate: the queue is worked from the
    /// longest-waiting request, unlike <see cref="GetByRequesterAsync"/>'s newest-first list.
    /// </summary>
    Task<IReadOnlyList<Briefing>> GetPendingApprovalAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts every briefing in <c>AwaitingApproval</c>, ignoring pagination — the full depth of the queue
    /// read by <see cref="GetPendingApprovalAsync"/>.
    /// </summary>
    Task<int> CountPendingApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the briefings the deadline monitor still has to judge (RF028, BRF-043): those not yet
    /// marked (<see cref="Briefing.OverdueSince"/> null) and still open — <c>AwaitingApproval</c>,
    /// <c>ChangesRequested</c>, <c>Approved</c> or <c>InProgress</c>. Ordered by <c>CreatedAt</c> then
    /// <c>Id</c>, so offset paging is stable across the pages of one sweep. The task-side counterpart is
    /// <see cref="IBriefingTaskRepository.GetOverdueCandidatesAsync"/>.
    /// </summary>
    /// <remarks>
    /// The four excluded statuses each have a reason: <see cref="BriefingStatus.Draft"/> was never submitted,
    /// so the design sector owes nothing on it; <see cref="BriefingStatus.Rejected"/>,
    /// <see cref="BriefingStatus.Cancelled"/> and <see cref="BriefingStatus.Completed"/> are closed. A
    /// briefing still awaiting review <b>is</b> swept: sitting unreviewed past the requested date is exactly
    /// the delay RF030 exists to surface.
    /// </remarks>
    Task<IReadOnlyList<Briefing>> GetOverdueCandidatesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the briefings the deadline monitor has already marked and that are still open (RF030,
    /// BRF-045): <see cref="Briefing.OverdueSince"/> filled and the status among the four the sector still
    /// answers for — <c>AwaitingApproval</c>, <c>ChangesRequested</c>, <c>Approved</c> or <c>InProgress</c>.
    /// Ordered by the oldest mark first, <c>Id</c> breaking ties — the queue is worked from whatever has been
    /// late the longest. The total behind the same predicate comes from <see cref="CountOverdueAsync"/>.
    /// </summary>
    /// <remarks>
    /// The complement of <see cref="GetOverdueCandidatesAsync"/> over the same four statuses: that one
    /// requires the mark to be null, this one requires it to be set. Neither recomputes lateness — deciding
    /// it is the monitor's job (BRF-043), and a second definition here could disagree with the
    /// <c>DeadlineOverdue</c> already published (RF035).
    /// <para>
    /// The closed statuses keep their mark on purpose — it records that the demand ran late — but stay out
    /// of this list, which exists to show what still needs acting on. Unlike the task side, where excluding
    /// <c>Approved</c> is enough (BRF-044), a briefing reaches three different closed states without ever
    /// clearing the mark, so the filter names the open ones instead. The way a briefing leaves the list is
    /// being edited, which clears the mark (<see cref="Briefing.Edit"/>).
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<Briefing>> GetOverdueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts every open briefing currently marked as late, ignoring pagination — the same predicate
    /// <see cref="GetOverdueAsync"/> pages through, and the number the SLA dashboard headlines (BRF-046).
    /// </summary>
    Task<int> CountOverdueAsync(CancellationToken cancellationToken = default);
}
