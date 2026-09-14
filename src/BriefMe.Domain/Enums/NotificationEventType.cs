namespace BriefMe.Domain.Enums;

/// <summary>
/// Why a person was notified (RF031–RF035, BRF-047). Stored on every <see cref="Entities.Notification"/> so
/// the inbox can group, filter and icon its entries without parsing the message text.
/// </summary>
/// <remarks>
/// One member per notification requirement, mirroring the event list BRF-049 consolidated
/// (<c>BriefingApproved</c>, <c>BriefingRejected</c>, <c>TaskAssigned</c>, <c>EntityChanged</c>,
/// <c>DeadlineOverdue</c>). It is deliberately <b>not</b> a copy of the domain event catalogue: this is the
/// reason the recipient sees, and several domain events legitimately map to the same reason —
/// <c>ChangesRequested</c> and a reassignment both reach the involved people as
/// <see cref="EntityChanged"/> (RF034, BRF-052).
/// <para>
/// Persisted as text (BRF-048), per the project's enum-as-string convention: a value read straight from the
/// <c>Notifications</c> table must be legible without a lookup.
/// </para>
/// </remarks>
public enum NotificationEventType
{
    /// <summary>The recipient's briefing was approved by a Design Manager (RF031, BRF-050).</summary>
    BriefingApproved = 0,

    /// <summary>The recipient's briefing was rejected, with a justification (RF032, BRF-050).</summary>
    BriefingRejected = 1,

    /// <summary>A task was assigned to the recipient (RF033, BRF-051).</summary>
    TaskAssigned = 2,

    /// <summary>
    /// Something relevant changed on a briefing or task the recipient is involved in (RF034, BRF-052):
    /// requester, assigned Designer or the Manager responsible for the approval.
    /// </summary>
    EntityChanged = 3,

    /// <summary>A demand the recipient is responsible for missed its deadline (RF035, BRF-053).</summary>
    DeadlineOverdue = 4
}
