using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// One row of a person's in-app inbox as the bell renders it (RF031–RF035, BRF-054): what the system said,
/// why, when, and whether they had already seen it.
/// </summary>
/// <remarks>
/// Carries the sentence the notification was created with, not a rendering of the aggregate it speaks of.
/// That is the entity's own decision (BRF-047): a rejection already read must not change its wording because
/// the briefing was edited afterwards, and a list of ten entries must not cost four modules' worth of reads
/// to draw.
/// <para>
/// <see cref="ReadAt"/> is nullable while the entry is unread, and <see cref="IsRead"/> derives from it
/// rather than being a second field free to disagree — the shape the entity has. The bell uses the flag to
/// mark what was new at the moment the panel opened, which is why the read instant travels to the client at
/// all.
/// </para>
/// <para>
/// How long ago it happened is deliberately absent: it is <see cref="CreatedAt"/> against the clock, and a
/// DTO factory is pure (<c>.claude/rules/dto-mapping.md</c>) — the screen formats the elapsed time, as it
/// does for <see cref="OverdueTaskDto"/>.
/// </para>
/// <para>
/// There is no link to the briefing or task behind the notice: the aggregate holds no subject id by design
/// (BRF-047, decision #5), and adding one is a schema change, not a mapping.
/// </para>
/// </remarks>
public sealed record NotificationDto(
    Guid Id,
    NotificationEventType EventType,
    string Title,
    string Message,
    DateTime CreatedAt,
    DateTime? ReadAt)
{
    /// <summary>Derived, exactly as on the entity — the client never gets two versions of the same fact.</summary>
    public bool IsRead => ReadAt is not null;

    public static NotificationDto FromNotification(Notification notification)
    {
        return new(
            notification.Id,
            notification.EventType,
            notification.Title,
            notification.Message,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
