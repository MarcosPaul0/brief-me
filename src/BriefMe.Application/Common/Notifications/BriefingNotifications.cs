using System.Globalization;
using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Notifications;

/// <summary>
/// Single source of truth for the wording of the briefing notifications (RF031, RF032, BRF-050; RF034,
/// BRF-052). The handlers reference it; they never declare the sentence themselves — same shape as
/// <see cref="Errors.BriefingErrors"/>, and the same reason: one concept, one place to reword it.
/// </summary>
/// <remarks>
/// Portuguese, unlike the briefing failure messages next door. These are read by a requester in the in-app
/// bell (BRF-054), in an interface that is Portuguese throughout — the vocabulary
/// <c>Web/Components/Briefings/BriefingLabels</c> already uses. They are also not part of the API contract,
/// so no endpoint test asserts on them.
/// <para>
/// Holds constants and pure methods only, so it does not break the "no static classes in Application"
/// convention for the same reason the error catalogs do not.
/// </para>
/// </remarks>
public static class BriefingNotifications
{
    /// <summary>Collapsed line of the bell: it names the event, never the briefing, so it cannot overflow.</summary>
    public const string ApprovedTitle = "Briefing aprovado";

    public const string RejectedTitle = "Briefing rejeitado";

    public static string Approved(string briefingTitle) =>
        Fit($"Seu briefing \"{briefingTitle}\" foi aprovado e segue para a criação das tarefas.");

    /// <summary>
    /// Carries the justification the Manager was required to give (RF013), so the requester knows what to
    /// fix without chasing anyone.
    /// </summary>
    public static string Rejected(string briefingTitle, string justification) =>
        Fit($"Seu briefing \"{briefingTitle}\" foi rejeitado. Justificativa: {justification}");

    /// <summary>Collapsed line for the four relevant changes a briefing can undergo (RF034, BRF-052).</summary>
    public const string EditedTitle = "Briefing alterado";

    public const string CancelledTitle = "Briefing cancelado";

    public const string CompletedTitle = "Briefing concluído";

    public const string ReopenedTitle = "Briefing reaberto";

    /// <summary>
    /// The briefing's content changed after it left the requester's desk (RF009, RF034). Read by the
    /// reviewing Manager and by the Designers working on it, never only by the requester — which is why the
    /// sentence says "o briefing", unlike <see cref="Approved"/>'s "seu briefing".
    /// </summary>
    /// <remarks>
    /// Names no field: <c>Briefing.Edit</c> moves six of them at once and the event carries none, so listing
    /// what changed would mean diffing the aggregate — the History module's job (RF036–RF038, BRF-057), and
    /// a heavier read than an alert deserves.
    /// <para>
    /// These four bodies get no <see cref="Fit"/>. A briefing title is capped at 200 by its EF configuration
    /// and nothing else here is free text, so the worst case sits near 260 against the 2000 of
    /// <see cref="Notification.MaxMessageLength"/> — the same reasoning as
    /// <see cref="BriefingTaskNotifications"/>, and the reason <see cref="Rejected"/> is the one that needs
    /// the cut: it carries a justification.
    /// </para>
    /// </remarks>
    public static string Edited(string briefingTitle) =>
        $"O briefing \"{briefingTitle}\" foi alterado.";

    public static string Cancelled(string briefingTitle) =>
        $"O briefing \"{briefingTitle}\" foi cancelado.";

    /// <summary>
    /// Every task of the briefing was approved and it closed itself (RF024, RF058, BRF-036). The clause
    /// after the colon says <i>why</i> it closed — completion is never a decision someone took, so without it
    /// the reader would look for whoever ended their demand.
    /// </summary>
    public static string Completed(string briefingTitle) =>
        $"O briefing \"{briefingTitle}\" foi concluído: todas as tarefas foram aprovadas.";

    /// <summary>
    /// A completed briefing came back into execution because one of its tasks was reopened (RF025, BRF-037).
    /// </summary>
    public static string Reopened(string briefingTitle) =>
        $"O briefing \"{briefingTitle}\" voltou para execução.";

    /// <summary>Collapsed line for a briefing the deadline monitor marked as late (RF035, BRF-053).</summary>
    public const string OverdueTitle = "Briefing em atraso";

    /// <summary>
    /// The briefing passed the deadline that applies to it (RF030, RF035) — the date the requester asked for
    /// or the SLA policy of its category and priority (RF027), whichever came first. Read by the requester
    /// and by the Manager who reviewed it.
    /// </summary>
    /// <remarks>
    /// Impersonal ("o briefing"), unlike <see cref="Approved"/>'s "seu briefing": the same sentence reaches
    /// the Manager, who is a responsible party here and not the owner of the demand.
    /// <para>
    /// Names when the deadline fell due rather than how long the briefing has been late, and prints the date
    /// without the time — both for the reasons written out on
    /// <see cref="BriefingTaskNotifications.Overdue"/>, which is the same sentence for the other kind of
    /// demand.
    /// </para>
    /// <para>
    /// No <see cref="Fit"/>: the title is capped at 200 by its EF configuration and the date is 10
    /// characters, so the worst case sits near 285 against the 2000 of
    /// <see cref="Notification.MaxMessageLength"/>. <see cref="Rejected"/> needs the cut because it carries
    /// a justification; nothing free-text reaches this body.
    /// </para>
    /// </remarks>
    /// <param name="briefingTitle">Title of the late briefing.</param>
    /// <param name="dueAtUtc">The instant the briefing was due, as resolved by the monitor (BRF-043).</param>
    public static string Overdue(string briefingTitle, DateTime dueAtUtc) =>
        $"O briefing \"{briefingTitle}\" está em atraso: " +
        $"o prazo venceu em {dueAtUtc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.";

    /// <summary>
    /// Cuts the body down to what <see cref="Notification"/> accepts, marking the cut.
    /// </summary>
    /// <remarks>
    /// Not defensive padding: a title at its 200-character limit plus a justification at
    /// <c>Justification.MaxLength</c> (2000) already exceeds <see cref="Notification.MaxMessageLength"/>
    /// (2000). Without the cut, <c>Notification.Create</c> would throw exactly for the Manager who wrote the
    /// most detail, the dispatcher would swallow it (BRF-049), and the requester would get no notice at all.
    /// A trimmed sentence beats a silent one.
    /// </remarks>
    private static string Fit(string message) =>
        message.Length <= Notification.MaxMessageLength
            ? message
            : string.Concat(message.AsSpan(0, Notification.MaxMessageLength - 1), "…");
}
