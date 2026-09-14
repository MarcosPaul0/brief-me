using BriefMe.Domain.Common;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when a Design Manager rejects a briefing (RF012/RF013, BRF-015). Carries the justification so the
/// Notifications module (BRF-050) can tell the requester why, without reloading the aggregate.
/// </summary>
public sealed record BriefingRejected(
    Guid BriefingId,
    Guid RequesterId,
    Guid RejectedById,
    string Justification) : IDomainEvent;
