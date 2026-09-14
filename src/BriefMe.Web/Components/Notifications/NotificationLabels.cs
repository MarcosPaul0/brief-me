using BriefMe.Domain.Enums;

namespace BriefMe.Web.Components.Notifications;

/// <summary>
/// Display text and treatment for an inbox entry (BRF-054, RF031–RF035): what to call the reason, how to
/// tint it, how long ago it happened and what the badge shows. Presentation only — the sentence itself was
/// written when the notification was created (<c>Application/Common/Notifications</c>) and is never
/// recomposed here.
/// </summary>
/// <remarks>
/// Pure statics rather than component methods, for the same reason as <see cref="Sla.OverdueLabels"/>: the
/// wording is worth testing without rendering anything, and a new <see cref="NotificationEventType"/> member
/// is then translated in exactly one place.
/// <para>
/// The relative time is derived here because <c>NotificationDto</c> deliberately carries the instant and not
/// the elapsed span — a DTO factory is pure and takes no clock (<c>.claude/rules/dto-mapping.md</c>).
/// </para>
/// </remarks>
public static class NotificationLabels
{
    /// <summary>Above this, the badge stops showing a number and starts showing that there are many.</summary>
    private const int MaxBadgeCount = 99;

    /// <summary>Past this many days, "há N dias" stops helping and the date is what the reader wants.</summary>
    private const int RelativeDayLimit = 7;

    /// <summary>
    /// Name of the reason the person was notified (RF031–RF035). Falls back to a neutral word rather than
    /// throwing on an unmapped member: a bell that crashes over a new enum value would hide every other
    /// entry with it.
    /// </summary>
    public static string EventType(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.BriefingApproved => "Aprovação",
        NotificationEventType.BriefingRejected => "Reprovação",
        NotificationEventType.TaskAssigned => "Atribuição",
        NotificationEventType.EntityChanged => "Alteração",
        NotificationEventType.DeadlineOverdue => "Atraso",
        _ => "Notificação"
    };

    /// <summary>
    /// Tint of the reason mark — the dot beside an entry and its eyebrow. One colour per reason, all of them
    /// from the palette already in use: success green for an approval, red for a rejection, the system's one
    /// blue for an assignment, muted grey for a generic change, and the same orange
    /// <see cref="Sla.OverdueLabels"/> gives a fresh delay.
    /// </summary>
    public static string EventTypeColorClass(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.BriefingApproved => "text-[#22c55e]",
        NotificationEventType.BriefingRejected => "text-[#ef4444]",
        NotificationEventType.TaskAssigned => "text-[#0099ff]",
        NotificationEventType.DeadlineOverdue => "text-[#ff7a3d]",
        _ => "text-[#999999]"
    };

    /// <summary>Same scale as <see cref="EventTypeColorClass"/>, as a background — the unread dot.</summary>
    public static string EventTypeDotClass(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.BriefingApproved => "bg-[#22c55e]",
        NotificationEventType.BriefingRejected => "bg-[#ef4444]",
        NotificationEventType.TaskAssigned => "bg-[#0099ff]",
        NotificationEventType.DeadlineOverdue => "bg-[#ff7a3d]",
        _ => "bg-[#999999]"
    };

    /// <summary>
    /// How long ago the notification was written, minute-grained near the present and coarser as it recedes;
    /// past <see cref="RelativeDayLimit"/> days it becomes the date, because "há 34 dias" is a number the
    /// reader has to convert back.
    /// </summary>
    /// <remarks>
    /// An instant in the future relative to the caller's clock — skew between the browser's reference and
    /// the server that stamped it — reads as "agora" rather than as a negative span: the entry exists, so it
    /// happened, and only the elapsed time is uncertain. Same reasoning as
    /// <see cref="Sla.OverdueLabels.Elapsed"/>.
    /// </remarks>
    public static string RelativeTime(DateTime createdAtUtc, DateTime nowUtc)
    {
        var elapsed = nowUtc - createdAtUtc;

        if (elapsed < TimeSpan.FromMinutes(1))
            return "agora";

        if (elapsed < TimeSpan.FromHours(1))
        {
            var minutes = (int)elapsed.TotalMinutes;
            return minutes == 1 ? "há 1 minuto" : $"há {minutes} minutos";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "há 1 hora" : $"há {hours} horas";
        }

        var days = (int)elapsed.TotalDays;

        if (days > RelativeDayLimit)
            return createdAtUtc.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        return days == 1 ? "há 1 dia" : $"há {days} dias";
    }

    /// <summary>
    /// What the badge prints. Capped so an inbox nobody has opened in a month cannot widen the sidebar
    /// chrome; zero and below print nothing, because the caller hides the badge entirely in that case.
    /// </summary>
    public static string UnreadBadge(int count) => count switch
    {
        <= 0 => string.Empty,
        > MaxBadgeCount => $"{MaxBadgeCount}+",
        _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };
}
