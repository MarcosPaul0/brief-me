namespace BriefMe.Domain.Enums;

/// <summary>
/// Which relevant change a <see cref="Events.EntityChanged"/> event reports (RF034, BRF-049). The consumer
/// (BRF-052) turns it into the sentence the involved people read.
/// </summary>
/// <remarks>
/// An enum rather than a free-form string: a <c>switch</c> over it stops compiling when a member is added,
/// so a change nobody wrote wording for cannot ship silently. Every member has exactly one raising site —
/// the list grows only when an aggregate gains an act worth telling people about.
/// </remarks>
public enum EntityChangeKind
{
    /// <summary>The briefing's content was edited after it left the requester's desk (RF009).</summary>
    Edited = 0,

    /// <summary>The briefing was cancelled before execution started (RF010).</summary>
    Cancelled = 1,

    /// <summary>The briefing was closed because every one of its tasks is approved (RF024, RF058).</summary>
    Completed = 2,

    /// <summary>A completed briefing, or an approved task, was brought back into execution (RF025).</summary>
    Reopened = 3,

    /// <summary>The task changed hands, from one Designer to another (RF026).</summary>
    Reassigned = 4,

    /// <summary>The task moved between the execution states of RF020.</summary>
    StatusChanged = 5,

    /// <summary>The task's deadline or priority was set or changed (RF018, RF019).</summary>
    Replanned = 6
}
