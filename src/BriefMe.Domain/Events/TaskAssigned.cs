using BriefMe.Domain.Common;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when a Design Manager assigns a task to a Designer (RF017, BRF-027). Consumed later by the
/// Notifications module (BRF-051) to alert the assignee (RF033). Collected on the aggregate and dispatched by
/// the publishing pipeline of BRF-049 on the commit that assigns it; no consumer exists yet.
/// </summary>
/// <remarks>
/// Carries <paramref name="BriefingId"/> so the notification handler can name the briefing the task belongs
/// to without reloading the aggregate — same reason <see cref="BriefingApproved"/> carries the requester.
/// </remarks>
public sealed record TaskAssigned(
    Guid TaskId,
    Guid BriefingId,
    Guid DesignerId,
    Guid AssignedById) : IDomainEvent;
