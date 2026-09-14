using BriefMe.Domain.Common;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when a Design Manager approves a briefing (RF011, BRF-015). Consumed later by the Notifications
/// module (BRF-050) to alert the requester. Collected on the aggregate and dispatched by the publishing
/// pipeline of BRF-049 on the commit that approves it; no consumer exists yet.
/// </summary>
public sealed record BriefingApproved(Guid BriefingId, Guid RequesterId, Guid ApprovedById) : IDomainEvent;
