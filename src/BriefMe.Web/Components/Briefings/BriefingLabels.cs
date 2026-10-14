using BriefMe.Domain.Enums;

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
    /// Hierarchy on the dark canvas is ink vs ink-muted; only states that ask for the reader's attention get a
    /// tint — a delivery waiting for review, and the two final outcomes.
    /// </summary>
    public static string StatusBadgeClass(BriefingStatus status) => status switch
    {
        BriefingStatus.Completed => "bg-[#22c55e]/10 text-[#22c55e]",
        BriefingStatus.Delivered => "bg-[#0099ff]/10 text-[#0099ff]",
        BriefingStatus.Cancelled => "bg-[#ef4444]/10 text-[#ef4444]",
        _ => "bg-[#1c1c1c] text-[#999999]"
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
