namespace BriefMe.Domain.Enums;

/// <summary>
/// Lifecycle of a briefing: the requester drafts and submits it, a designer claims it from the queue and
/// delivers, and the requester accepts the delivery or sends it back with what to change.
/// </summary>
public enum BriefingStatus
{
    /// <summary>Being written by the requester; nobody else sees it.</summary>
    Draft = 1,

    /// <summary>In the open queue, waiting for a designer to claim it. Still editable and cancellable.</summary>
    Submitted = 2,

    /// <summary>Claimed by a designer, who is working on it (also after a change request).</summary>
    InProgress = 3,

    /// <summary>The designer delivered; waiting for the requester to accept or ask for changes.</summary>
    Delivered = 4,

    /// <summary>The requester accepted the delivery. Final.</summary>
    Completed = 5,

    /// <summary>The requester gave up before anyone claimed it. Final.</summary>
    Cancelled = 6
}
