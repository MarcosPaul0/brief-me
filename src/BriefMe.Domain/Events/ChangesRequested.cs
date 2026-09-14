using BriefMe.Domain.Common;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when a Design Manager returns a briefing to the requester for corrections (RF014, BRF-015). Named
/// after the backlog event of BRF-015; it is a distinct type from the <c>BriefingStatus.ChangesRequested</c>
/// enum member. Consumed later by the Notifications module (BRF-052).
/// </summary>
public sealed record ChangesRequested(
    Guid BriefingId,
    Guid RequesterId,
    Guid RequestedById,
    string Justification) : IDomainEvent;
