namespace BriefMe.Domain.Enums;

/// <summary>
/// Which of the two kinds of demand an event speaks of — the briefing itself or one of its tasks. Shared by
/// <see cref="Events.DeadlineOverdue"/> (RF028/RF035, BRF-043) and <see cref="Events.EntityChanged"/>
/// (RF034, BRF-049).
/// </summary>
/// <remarks>
/// One enum for both because the consumers are the same handlers (BRF-052, BRF-053), asking the same
/// question: whose work is this, and which briefing does it belong to. They are separate aggregates and the
/// person notified differs — the assigned Designer for a task (RF017), the requester for a briefing — which
/// is precisely why the event has to say which one it is.
/// </remarks>
public enum DemandSubject
{
    /// <summary>The event is about the briefing itself (RF030).</summary>
    Briefing = 0,

    /// <summary>The event is about a task of a briefing (RF029).</summary>
    Task = 1
}
