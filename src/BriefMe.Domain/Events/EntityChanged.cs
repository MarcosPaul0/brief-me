using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when a briefing or one of its tasks undergoes a change the people involved should hear about
/// (RF034, BRF-049). Consumed by the relevant-change notification handler (BRF-052), which resolves
/// "involved" as the requester, the assigned Designer(s) and the Manager responsible for approval.
/// </summary>
/// <remarks>
/// Raised by the aggregate method that performs the act, never inferred from what the <c>ChangeTracker</c>
/// saw: RF034 asks for the <b>relevant</b> changes, and only the method that executes one knows whether it is
/// one. Comparing property values is the job of the history module (RF036–RF038, BRF-057), which wants every
/// change, including the ones nobody needs to be told about.
/// <para>
/// Deliberately absent where a dedicated event already covers the act — approval, rejection, change request
/// (BRF-015), first assignment (BRF-027) and an overdue deadline (BRF-043) all have their own, and a second
/// event here would notify the same person twice for the same fact.
/// </para>
/// </remarks>
/// <param name="Subject">Whether the changed demand is the briefing itself or one of its tasks.</param>
/// <param name="SubjectId">Id of the changed demand — the task or the briefing, per <paramref name="Subject"/>.</param>
/// <param name="BriefingId">
/// Briefing the demand belongs to. Equal to <paramref name="SubjectId"/> for
/// <see cref="DemandSubject.Briefing"/>, so the consumer can find the involved people without reloading the
/// aggregate — same reason <see cref="TaskAssigned"/> and <see cref="DeadlineOverdue"/> carry it.
/// </param>
/// <param name="Change">Which act happened, and therefore what the notification says.</param>
/// <param name="ChangedById">
/// Who performed the change, when the aggregate method knows. Null for the acts whose methods take no actor
/// today (editing, cancelling, replanning, moving a task's status): the consumer uses this only to leave the
/// author out of their own notification, so an unknown author costs one redundant message rather than a
/// signature change across six use cases (BRF-049 design decision #8).
/// </param>
public sealed record EntityChanged(
    DemandSubject Subject,
    Guid SubjectId,
    Guid BriefingId,
    EntityChangeKind Change,
    Guid? ChangedById) : IDomainEvent;
