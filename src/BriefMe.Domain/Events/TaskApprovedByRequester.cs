using BriefMe.Domain.Common;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when the requester gives a task delivery its final approval (RF046), the last gate of the delivery
/// flow. Consumed by the automatic briefing completion rule (RF024/RF058, BRF-036), which checks whether this
/// was the briefing's last pending task.
/// </summary>
/// <remarks>
/// The event is defined by its consumer's slice (BRF-036) and raised by
/// <c>BriefingTask.ApproveByRequester</c> (RF046, BRF-065/BRF-069), which does not exist yet — so nothing
/// publishes it today. Like every other event of this domain it is collected on the aggregate and dispatched
/// by the publishing pipeline of BRF-049, on the commit that approves the delivery.
/// <para>
/// Carries <paramref name="BriefingId"/> because the completion rule reasons about the briefing, not the
/// task: without it the handler would have to load the task only to read one field — same reason
/// <see cref="TaskAssigned"/> carries it.
/// </para>
/// </remarks>
public sealed record TaskApprovedByRequester(
    Guid TaskId,
    Guid BriefingId,
    Guid ApprovedById) : IDomainEvent;
