using BriefMe.Domain.Enums;

namespace BriefMe.Web.Components.Tasks;

/// <summary>
/// Display text and treatment for <see cref="BriefingTaskStatus"/>, used by the Designer's task board
/// (BRF-033) and by whatever task screen comes next (BRF-034). Presentation only — the enum stays the
/// domain's vocabulary; this is the one place that translates it, so a new status is labelled once.
/// </summary>
/// <remarks>
/// <see cref="Priority"/> is not repeated here: it is the same domain enum the briefing screens show, and
/// <c>BriefingLabels.Priority</c> already translates it. One enum, one translation.
/// </remarks>
public static class BriefingTaskLabels
{
    public static string Status(BriefingTaskStatus status) => status switch
    {
        BriefingTaskStatus.NotStarted => "Não iniciada",
        BriefingTaskStatus.InProgress => "Em execução",
        BriefingTaskStatus.AwaitingTechnicalApproval => "Aguardando aprovação técnica",
        BriefingTaskStatus.AwaitingRequesterApproval => "Aguardando aprovação do solicitante",
        BriefingTaskStatus.Approved => "Aprovada",
        BriefingTaskStatus.Rejected => "Rejeitada",
        BriefingTaskStatus.Blocked => "Bloqueada",
        _ => status.ToString()
    };

    /// <summary>
    /// Colour is spent on meaning, never decoration (design system). Three statuses earn a tint: the
    /// terminal outcomes — <c>Approved</c> and <c>Rejected</c> — and <c>Blocked</c>, the state that is
    /// waiting on someone else and that BRF-033 asks the board to highlight. The rest carry hierarchy
    /// through ink vs ink-muted.
    /// </summary>
    public static string StatusBadgeClass(BriefingTaskStatus status) => status switch
    {
        BriefingTaskStatus.Approved => "bg-[#22c55e]/10 text-[#22c55e]",
        BriefingTaskStatus.Rejected => "bg-[#ef4444]/10 text-[#ef4444]",
        BriefingTaskStatus.Blocked => "bg-[#ff7a3d]/15 text-[#ff7a3d]",
        _ => "bg-[#1c1c1c] text-[#999999]"
    };

    /// <summary>
    /// Row treatment on the board. Only a blocked task changes the row itself — the badge alone is easy to
    /// miss when scanning a deadline-ordered plan, and the block is the one row the Designer cannot resolve
    /// by working.
    /// </summary>
    public static string RowClass(BriefingTaskStatus status) => status switch
    {
        BriefingTaskStatus.Blocked => "bg-[#ff7a3d]/[0.06]",
        _ => string.Empty
    };
}
