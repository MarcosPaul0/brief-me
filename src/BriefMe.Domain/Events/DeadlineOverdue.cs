using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when the deadline monitor finds a demand past its due instant (RF028, BRF-043). Consumed by the
/// overdue notification handler (BRF-053), which warns the people who answer for the demand (RF035): the
/// owner named by <see cref="ResponsibleId"/> and the Design Manager who approved the briefing.
/// </summary>
/// <remarks>
/// One event for both kinds of demand, told apart by <paramref name="Subject"/>: BRF-049 lists a single
/// <c>DeadlineOverdue</c>, and a handler that notifies "the person responsible for a late demand" reads the
/// same three fields either way. <see cref="EntityChanged"/> tells them apart the same way.
/// <para>
/// Raised exactly once per breach, and that guarantee is not the dispatcher's: the monitor's query excludes
/// demands already marked, so a marked one is never evaluated again. Replanning the demand clears the mark
/// (<c>BriefingTask.SetDeadlineAndPriority</c>, <c>Briefing.Edit</c>) and a second breach is a second
/// event — which is the point, not a leak.
/// </para>
/// </remarks>
/// <param name="Subject">Whether the late demand is the briefing itself or one of its tasks.</param>
/// <param name="SubjectId">Id of the late demand — the task or the briefing, per <paramref name="Subject"/>.</param>
/// <param name="BriefingId">
/// Briefing the demand belongs to. Equal to <paramref name="SubjectId"/> for
/// <see cref="DemandSubject.Briefing"/>, so the consumer can name the demand without reloading anything —
/// same reason <see cref="TaskAssigned"/> carries it.
/// </param>
/// <param name="ResponsibleId">
/// Who is expected to act: the assigned Designer for a task (RF017), the requester for a briefing. Null when
/// a task has not been assigned yet (RF018 plans a task before RF017 gives it an owner), which is precisely
/// a late demand nobody holds — the consumer of BRF-053 covers that case with the reviewing Manager, so the
/// alert is never announced to no one.
/// </param>
/// <param name="DueAtUtc">
/// The instant the demand was due — the earliest of its own deadline and its SLA. This, not
/// <paramref name="DetectedAtUtc"/>, is what the notification names: a stored message never recomputes
/// itself, so a due date stays true where an elapsed delay would go stale (BRF-053).
/// </param>
/// <param name="DetectedAtUtc">When the monitor found the breach; the value stored in <c>OverdueSince</c>.</param>
public sealed record DeadlineOverdue(
    DemandSubject Subject,
    Guid SubjectId,
    Guid BriefingId,
    Guid? ResponsibleId,
    DateTime DueAtUtc,
    DateTime DetectedAtUtc) : IDomainEvent;
