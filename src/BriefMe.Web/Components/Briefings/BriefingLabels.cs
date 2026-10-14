using BriefMe.Domain.Enums;
using BriefMe.Web.Ui;

namespace BriefMe.Web.Components.Briefings;

/// <summary>
/// Display text for the briefing enums, shared by the screens that show them. Presentation only — the enums themselves stay the domain's vocabulary; this is
/// the one place that translates them, so a new status is labelled once.
/// </summary>
public static class BriefingLabels
{
    public static string Status(BriefingStatus status) => status switch
    {
        BriefingStatus.Draft => "Rascunho",
        BriefingStatus.Submitted => "Na fila",
        BriefingStatus.InProgress => "Em andamento",
        BriefingStatus.Delivered => "Entregue",
        BriefingStatus.Completed => "Concluído",
        BriefingStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    /// <summary>
    /// Only states that ask for the reader's attention get a tint — a delivery waiting for review, and the two
    /// final outcomes; the rest stay neutral. What each variant looks like is the <see cref="Badge"/>'s call.
    /// </summary>
    public static BadgeVariant StatusBadgeVariant(BriefingStatus status) => status switch
    {
        BriefingStatus.Completed => BadgeVariant.Success,
        BriefingStatus.Delivered => BadgeVariant.Info,
        BriefingStatus.Cancelled => BadgeVariant.Danger,
        _ => BadgeVariant.Neutral
    };

    public static string Priority(Priority priority) => priority switch
    {
        Domain.Enums.Priority.Low => "Baixa",
        Domain.Enums.Priority.Medium => "Média",
        Domain.Enums.Priority.High => "Alta",
        Domain.Enums.Priority.Urgent => "Urgente",
        _ => priority.ToString()
    };
}
