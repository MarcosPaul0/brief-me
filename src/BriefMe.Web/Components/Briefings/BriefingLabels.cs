using BriefMe.Domain.Enums;

namespace BriefMe.Web.Components.Briefings;

/// <summary>
/// Display text for the briefing enums, shared by the screens that show them (BRF-014's requester list and
/// BRF-020's approval panel). Presentation only — the enums themselves stay the domain's vocabulary; this is
/// the one place that translates them, so a new status is labelled once.
/// </summary>
public static class BriefingLabels
{
    public static string Status(BriefingStatus status) => status switch
    {
        BriefingStatus.Draft => "Rascunho",
        BriefingStatus.AwaitingApproval => "Aguardando aprovação",
        BriefingStatus.ChangesRequested => "Ajustes solicitados",
        BriefingStatus.Approved => "Aprovado",
        BriefingStatus.Rejected => "Rejeitado",
        BriefingStatus.InProgress => "Em execução",
        BriefingStatus.Completed => "Concluído",
        BriefingStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    /// <summary>
    /// Hierarchy on the dark canvas is ink vs ink-muted; only the two terminal outcomes get a tint
    /// (design system: one signal colour per meaning, never decorative).
    /// </summary>
    public static string StatusBadgeClass(BriefingStatus status) => status switch
    {
        BriefingStatus.Completed or BriefingStatus.Approved => "bg-[#22c55e]/10 text-[#22c55e]",
        BriefingStatus.Rejected or BriefingStatus.Cancelled => "bg-[#ef4444]/10 text-[#ef4444]",
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
