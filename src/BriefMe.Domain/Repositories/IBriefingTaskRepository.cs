using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

/// <summary>
/// Access to the <see cref="BriefingTask"/> aggregate (BRF-025). Named after the entity rather than after the
/// backlog's <c>ITaskRepository</c>, for the same reason the entity is not called <c>Task</c> — see
/// <c>docs/modelagem-tarefas-comentarios.md</c>. The table is still <c>Tasks</c>.
/// </summary>
public interface IBriefingTaskRepository : IRepository<BriefingTask>
{
    /// <summary>
    /// Returns every task of a briefing (RF016), oldest first — the order in which the Design Manager broke
    /// the briefing down. Deliberately not paginated: the progress calculation (RF023/RF059, BRF-035) and the
    /// automatic completion rule (RF024/RF058, BRF-036) divide approved tasks by the total, so a truncated
    /// page would yield a wrong percentage.
    /// </summary>
    Task<IReadOnlyList<BriefingTask>> GetByBriefingIdAsync(
        Guid briefingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one task with its comment trail loaded (RF021, BRF-034), oldest comment first — the order
    /// the conversation was written in. <see cref="IRepository{T}.GetByIdAsync"/> leaves
    /// <see cref="BriefingTask.Comments"/> empty, so the write use cases that only need the root keep
    /// paying for nothing; the detail screen, which renders the trail, asks for it here.
    /// </summary>
    /// <remarks>
    /// The task's delivery files are <b>not</b> here: an <see cref="Attachment"/> is a child of the
    /// <see cref="Briefing"/> aggregate (BRF-004), so it is read through
    /// <see cref="IBriefingRepository.GetDeliveriesByTaskAsync"/>.
    /// </remarks>
    Task<BriefingTask?> GetByIdWithCommentsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of the tasks assigned to a designer (RF017), filtered, ordered by the nearest
    /// <see cref="BriefingTask.Deadline"/> first with undated tasks last (BRF-033). The order differs from
    /// <see cref="IBriefingRepository.GetByRequesterAsync"/>'s newest-first on purpose: a requester reads a
    /// history, a designer reads a work plan. A task with no deadline has not been planned yet (RF018) and
    /// must not push a dated one down. The total behind the filter comes from
    /// <see cref="CountByDesignerAsync"/>.
    /// <c>GetByIdAsync</c> and <c>AddAsync</c> come from <see cref="IRepository{T}"/>.
    /// </summary>
    Task<IReadOnlyList<BriefingTask>> GetByDesignerAsync(
        Guid designerId,
        BriefingTaskFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the designer's tasks matching <paramref name="filter"/>, ignoring pagination. Same filter the
    /// caller passed to <see cref="GetByDesignerAsync"/>.
    /// </summary>
    Task<int> CountByDesignerAsync(
        Guid designerId,
        BriefingTaskFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the tasks the deadline monitor still has to judge (RF028, BRF-043): those not yet marked
    /// (<see cref="BriefingTask.OverdueSince"/> null) and not yet finished
    /// (<see cref="BriefingTaskStatus.Approved"/> excluded). Ordered by <c>CreatedAt</c> then <c>Id</c>, so
    /// offset paging is stable across the pages of one sweep.
    /// </summary>
    /// <remarks>
    /// The null filter is what makes the sweep idempotent, and it is deliberately in the <b>query</b> rather
    /// than in the loop: a marked task is never loaded again, so "the overdue event is published exactly
    /// once" is a fact of the database instead of a promise of the dispatcher.
    /// <para>
    /// <see cref="BriefingTaskStatus.Blocked"/> is included: blocking does not stop the clock, and a blocked
    /// task past its deadline is precisely what the Design Manager needs to see. Whether a candidate is
    /// actually late cannot be decided here — it depends on the SLA rules (RF027), which live in another
    /// aggregate — so the caller resolves that with <see cref="Services.SlaDeadlineResolver"/>.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<BriefingTask>> GetOverdueCandidatesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the tasks the deadline monitor has already marked and that are still open (RF029,
    /// BRF-044): <see cref="BriefingTask.OverdueSince"/> filled and <see cref="BriefingTaskStatus.Approved"/>
    /// excluded. Ordered by the oldest mark first, <c>Id</c> breaking ties — the queue is worked from
    /// whatever has been late the longest. The total behind the same predicate comes from
    /// <see cref="CountOverdueAsync"/>.
    /// </summary>
    /// <remarks>
    /// The complement of <see cref="GetOverdueCandidatesAsync"/>, which reads the tasks still to be judged:
    /// that one requires the mark to be null, this one requires it to be set. Neither recomputes lateness —
    /// deciding it is the monitor's job (BRF-043), and a second definition here could disagree with the
    /// <c>DeadlineOverdue</c> already published (RF035).
    /// <para>
    /// Approved tasks keep their mark on purpose — it records that the delivery came out late — but stay out
    /// of this list, which exists to show what still needs acting on. The way a task leaves the list is
    /// replanning, which clears the mark (<see cref="BriefingTask.SetDeadlineAndPriority"/>).
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<BriefingTask>> GetOverdueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts every open task currently marked as late, ignoring pagination — the same predicate
    /// <see cref="GetOverdueAsync"/> pages through, and the number the SLA dashboard headlines (BRF-046).
    /// </summary>
    Task<int> CountOverdueAsync(CancellationToken cancellationToken = default);
}
