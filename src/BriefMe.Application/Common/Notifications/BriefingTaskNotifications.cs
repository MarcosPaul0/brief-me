using System.Globalization;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Notifications;

/// <summary>
/// Single source of truth for the wording of the task notifications (RF033, BRF-051; RF034, BRF-052). The
/// handlers reference it; they never declare the sentence themselves — same shape as
/// <see cref="BriefingNotifications"/> next door, and the same split as <c>Errors/BriefingTaskErrors</c>:
/// one catalog per feature owning the concept.
/// </summary>
/// <remarks>
/// Portuguese, like the briefing notifications and unlike the failure messages: these are read by a Designer
/// in the in-app bell (BRF-054), in an interface that is Portuguese throughout, and they are not part of the
/// API contract.
/// <para>
/// Holds constants and pure methods only, so it does not break the "no static classes in Application"
/// convention for the same reason the error catalogs do not.
/// </para>
/// </remarks>
public static class BriefingTaskNotifications
{
    /// <summary>Collapsed line of the bell: it names the event, never the task, so it cannot overflow.</summary>
    public const string AssignedTitle = "Nova tarefa atribuída";

    /// <summary>
    /// Announces the assignment to the Designer who received it (RF033), naming the task and — when the
    /// handler could resolve it — the briefing it belongs to. On a board where "Banner" and "Post" repeat
    /// across demands, the task title alone does not say which work arrived.
    /// </summary>
    /// <remarks>
    /// One method with a nullable briefing title rather than two: the difference between the two sentences is
    /// a clause, not another notice, and there is a single caller.
    /// <para>
    /// No length trimming here, unlike <see cref="BriefingNotifications"/>. Both titles are capped at 200 by
    /// their EF configurations, so the worst case sits around 445 characters against the 2000 of
    /// <see cref="Notification.MaxMessageLength"/> — a cut would guard an input that cannot exist. It comes
    /// back the day this body carries free text (a comment, a justification).
    /// </para>
    /// </remarks>
    /// <param name="taskTitle">Title of the assigned task.</param>
    /// <param name="briefingTitle">Title of the briefing the task belongs to, or <c>null</c> if it could not be read.</param>
    public static string Assigned(string taskTitle, string? briefingTitle) =>
        briefingTitle is null
            ? $"A tarefa \"{taskTitle}\" foi atribuída a você."
            : $"A tarefa \"{taskTitle}\" do briefing \"{briefingTitle}\" foi atribuída a você.";

    /// <summary>Collapsed line for the four relevant changes a task can undergo (RF034, BRF-052).</summary>
    public const string ReassignedTitle = "Tarefa reatribuída";

    public const string StatusChangedTitle = "Status de tarefa alterado";

    public const string ReplannedTitle = "Tarefa replanejada";

    public const string ReopenedTitle = "Tarefa reaberta";

    /// <summary>
    /// The task changed hands (RF026, RF034, BRF-038). Impersonal, unlike <see cref="Assigned"/>: the
    /// audience here is the requester and the reviewing Manager, never the Designer who received it — they
    /// hear it as <see cref="Assigned"/> on the <c>TaskAssigned</c> raised by the same act (BRF-051).
    /// </summary>
    /// <remarks>
    /// Names no Designer, old or new. Turning the two ids into names would cost a user lookup per
    /// notification for a fact the reader can see on the task screen, and the previous owner is not in the
    /// event at all.
    /// <para>
    /// None of these four bodies is trimmed, for the reason given on <see cref="Assigned"/>: both titles are
    /// capped at 200 by their EF configurations and nothing else here is free text, so the longest of them
    /// (<see cref="Replanned"/>) stays near 480 against the 2000 of
    /// <see cref="Notification.MaxMessageLength"/>.
    /// </para>
    /// </remarks>
    public static string Reassigned(string taskTitle, string briefingTitle) =>
        $"A tarefa \"{taskTitle}\" do briefing \"{briefingTitle}\" foi reatribuída a outro designer.";

    /// <summary>
    /// The task moved between the execution states of RF020. Names the new state, which is the whole point of
    /// the notice: it is how a task reaching <see cref="BriefingTaskStatus.AwaitingRequesterApproval"/> tells
    /// the requester there is a delivery waiting on them (RF045, BRF-068).
    /// </summary>
    public static string StatusChanged(string taskTitle, string briefingTitle, BriefingTaskStatus status) =>
        $"A tarefa \"{taskTitle}\" do briefing \"{briefingTitle}\" agora está em \"{Status(status)}\".";

    /// <summary>
    /// The task's deadline or priority was set or changed (RF018, RF019, BRF-028). Both are named because
    /// planning moves them together and the notice cannot say which of the two actually differs — the event
    /// carries no previous value, and comparing them is the History module's job (BRF-057).
    /// </summary>
    /// <param name="deadline">
    /// The new deadline. Never null in practice — <c>BriefingTask.SetDeadlineAndPriority</c> requires one —
    /// but <c>BriefingTask.Deadline</c> is nullable, so an absent date drops its clause instead of printing
    /// <c>01/01/0001</c>.
    /// </param>
    public static string Replanned(
        string taskTitle,
        string briefingTitle,
        DateOnly? deadline,
        Priority priority)
    {
        var subject = $"A tarefa \"{taskTitle}\" do briefing \"{briefingTitle}\" foi replanejada";

        // Invariant culture with an explicit pattern: the sentence must read the same whatever locale the
        // server happens to run under, and the reader is Brazilian.
        return deadline is null
            ? $"{subject}: prioridade {PriorityName(priority)}."
            : $"{subject}: entrega em {deadline.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}, " +
              $"prioridade {PriorityName(priority)}.";
    }

    /// <summary>An approved task was brought back into execution for corrections (RF025, BRF-037).</summary>
    public static string Reopened(string taskTitle, string briefingTitle) =>
        $"A tarefa \"{taskTitle}\" do briefing \"{briefingTitle}\" foi reaberta para ajustes.";

    /// <summary>Collapsed line for a task the deadline monitor marked as late (RF035, BRF-053).</summary>
    public const string OverdueTitle = "Tarefa em atraso";

    /// <summary>
    /// The task passed the deadline that applies to it (RF028, RF035) — its own typed date (RF018) or the
    /// SLA policy of its category and priority (RF027), whichever came first. Read by the assigned Designer
    /// and by the Manager who approved the briefing.
    /// </summary>
    /// <remarks>
    /// Names <b>when the deadline fell due</b>, never how long the task has been late. A
    /// <see cref="Notification"/> stores frozen text and never recomputes it, so "atrasada há 1 dia" is
    /// already false the next morning while "o prazo venceu em 05/09/2026" stays true forever. The elapsed
    /// delay is derived on screen against the reader's own clock instead —
    /// <c>Web/Components/Sla/OverdueLabels</c> (BRF-046) is the other half of that split.
    /// <para>
    /// Date without time, in the invariant culture. <paramref name="dueAtUtc"/> is a UTC instant, and
    /// printing "14:30" to a reader in UTC−3 without naming the zone would simply be wrong; converting the
    /// zone is a presentation decision (BRF-054), and this catalog stays pure — no clock, no configuration,
    /// exactly like the DTO factories. Day granularity is what <c>OverdueLabels</c> already settled on, for
    /// the reason recorded there: the monitor runs on a period of minutes and the decision this feeds is not
    /// sharper than a day.
    /// </para>
    /// <para>
    /// No length trimming, for the reason given on <see cref="Assigned"/>: both titles are capped at 200 by
    /// their EF configurations and the date is 10 characters, so the worst case sits near 485 against the
    /// 2000 of <see cref="Notification.MaxMessageLength"/>.
    /// </para>
    /// </remarks>
    /// <param name="taskTitle">Title of the late task.</param>
    /// <param name="briefingTitle">Title of the briefing it belongs to.</param>
    /// <param name="dueAtUtc">The instant the task was due, as resolved by the monitor (BRF-043).</param>
    public static string Overdue(string taskTitle, string briefingTitle, DateTime dueAtUtc) =>
        $"A tarefa \"{taskTitle}\" do briefing \"{briefingTitle}\" está em atraso: " +
        $"o prazo venceu em {dueAtUtc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.";

    /// <summary>
    /// Portuguese name of a task status, as it reads inside a sentence (RF034).
    /// </summary>
    /// <remarks>
    /// Deliberately duplicated from <c>Web/Components/Tasks/BriefingTaskLabels.Status</c>, which cannot be
    /// reused: Web references Application and never the other way round (CLAUDE.md), and moving the
    /// translation into Domain would put presentation inside the domain. Two vocabularies with different
    /// owners — a badge label there, a word inside a sentence here — kept in step by hand. Adding a status to
    /// <see cref="BriefingTaskStatus"/> means writing it in both places.
    /// </remarks>
    private static string Status(BriefingTaskStatus status) => status switch
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
    /// Portuguese name of a priority. Same duplication, same reason as <see cref="Status"/> — the counterpart
    /// there is <c>Web/Components/Briefings/BriefingLabels.Priority</c>.
    /// </summary>
    private static string PriorityName(Priority priority) => priority switch
    {
        Priority.Low => "Baixa",
        Priority.Medium => "Média",
        Priority.High => "Alta",
        Priority.Urgent => "Urgente",
        _ => priority.ToString()
    };
}
