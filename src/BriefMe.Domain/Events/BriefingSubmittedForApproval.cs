using BriefMe.Domain.Common;

namespace BriefMe.Domain.Events;

/// <summary>
/// Raised when a briefing transitions to <c>AwaitingApproval</c> (RF007, BRF-007). Consumed later by the
/// Notifications module (BRF-050) to alert the Design Manager responsible for approval. Collected on the
/// aggregate and dispatched by the publishing pipeline of BRF-049; no consumer exists yet.
/// </summary>
public sealed record BriefingSubmittedForApproval(Guid BriefingId, Guid RequesterId) : IDomainEvent;
