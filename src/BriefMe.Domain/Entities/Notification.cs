using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

/// <summary>
/// One entry in a person's in-app inbox (RF031–RF035, BRF-047): what happened, told to whom, and whether
/// they have seen it. Written by the notification handlers of BRF-050–053 and read by the bell of BRF-054.
/// </summary>
/// <remarks>
/// Aggregate root with a life of its own: a notification survives whatever produced it. Rejecting a briefing
/// twice writes two entries, and editing the briefing afterwards does not rewrite the sentence the requester
/// already read — which is why the entry copies <see cref="Title"/> and <see cref="Message"/> instead of
/// pointing at the aggregate and rendering it on display.
/// <para>
/// The text is immutable; only the read mark moves (<see cref="MarkAsRead"/>). There is no
/// <c>Notification.Edit</c> and no delete: an inbox is a record of what the system said, in the same spirit
/// as <see cref="Comment"/> being append-only.
/// </para>
/// <para>
/// Holds no foreign key to the briefing or task it speaks of. The demand it refers to may be in any module —
/// briefing, task, delivery — and a nullable column per module would be a schema that grows with every new
/// notification reason. Deep-linking the bell to the demand is BRF-054's problem and can add the pair
/// (subject type, subject id) when there is a screen asking for it.
/// </para>
/// </remarks>
public class Notification : AggregateRoot
{
    public const int MaxTitleLength = 200;

    public const int MaxMessageLength = 2000;

    private Notification(
        Guid id,
        Guid recipientId,
        NotificationEventType eventType,
        string title,
        string message,
        DateTime createdAt) : base(id)
    {
        RecipientId = recipientId;
        EventType = eventType;
        Title = title;
        Message = message;
        CreatedAt = createdAt;
    }

    private Notification()
    {
    }

    /// <summary>
    /// Who is being told. Logical FK to <see cref="User"/>; one entry per recipient, so telling three people
    /// about the same change (RF034) writes three rows, each with its own read mark.
    /// </summary>
    public Guid RecipientId { get; private set; }

    /// <summary>Why the recipient was notified (RF031–RF035).</summary>
    public NotificationEventType EventType { get; private set; }

    /// <summary>One-line summary, the only text the bell shows collapsed (BRF-054).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The full sentence — a rejection carries its justification here (RF013/RF032).</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>When the system said it. Orders the inbox; never changes.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the recipient read it, or <c>null</c> while unread. An instant rather than a boolean, for the
    /// same reason as <see cref="Briefing.OverdueSince"/>: it costs the same column, makes
    /// <see cref="MarkAsRead"/> idempotent by construction, and answers "how long did this sit unread"
    /// without a second field.
    /// </summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>Derived from <see cref="ReadAt"/>; not persisted — a second column would be free to diverge.</summary>
    public bool IsRead => ReadAt is not null;

    /// <summary>
    /// Creates an inbox entry for <paramref name="recipientId"/> (RF031–RF035). Text is required and
    /// trimmed: an entry nobody can read is not a notification, and the bell has no room for padding.
    /// </summary>
    /// <remarks>
    /// Throws rather than returning <c>Result</c>, unlike the use cases that answer a user: the caller here
    /// is a domain event handler (BRF-050–053) composing its own sentence from an event it already holds, so
    /// empty text is a bug in the handler, not input to be validated back to someone.
    /// </remarks>
    public static Notification Create(
        Guid recipientId,
        NotificationEventType eventType,
        string? title,
        string? message)
    {
        if (recipientId == Guid.Empty)
            throw new DomainException("Notification recipient is required.");

        if (!Enum.IsDefined(eventType))
            throw new DomainException($"Unknown notification event type: {(int)eventType}.");

        return new Notification(
            Guid.NewGuid(),
            recipientId,
            eventType,
            Normalize(title, MaxTitleLength, "title"),
            Normalize(message, MaxMessageLength, "message"),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Marks the entry as read (BRF-054). Idempotent: re-reading keeps the first instant, so opening the bell
    /// again — the very gesture that marks a whole page at once — never rewrites when it happened.
    /// </summary>
    public void MarkAsRead()
    {
        if (ReadAt is not null)
            return;

        ReadAt = DateTime.UtcNow;
    }

    private static string Normalize(string? text, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException($"Notification {field} is required.");

        var normalized = text.Trim();

        if (normalized.Length > maxLength)
            throw new DomainException($"Notification {field} must be at most {maxLength} characters.");

        return normalized;
    }
}
