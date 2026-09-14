namespace BriefMe.Web.Components.Sla;

/// <summary>
/// Display text and treatment for how long an item has been late (BRF-046, RF029/RF030). The elapsed delay
/// is deliberately absent from <c>OverdueTaskDto</c>/<c>OverdueBriefingDto</c>: a DTO factory is pure and
/// takes no clock (<c>.claude/rules/dto-mapping.md</c>), so the screen derives it from <c>OverdueSince</c>.
/// Presentation only — who is late stays the SLA monitor's decision (BRF-043).
/// </summary>
/// <remarks>
/// Pure statics over two instants rather than a component method: the wording is what the acceptance
/// criterion of BRF-046 shows, so it is worth testing without rendering anything, and both panels translate
/// it once.
/// </remarks>
public static class OverdueLabels
{
    /// <summary>Delay at which the wording turns from warning to breach, in days.</summary>
    private const int CriticalDays = 3;

    /// <summary>
    /// Day-grained wording for the delay between <paramref name="overdueSince"/> and
    /// <paramref name="nowUtc"/>. Day-grained because the monitor runs on a period of minutes and the
    /// decision this feeds — what to escalate first — is not sharper than a day.
    /// </summary>
    /// <remarks>
    /// A mark in the future relative to the caller's clock (skew between the browser and the server that
    /// wrote it) reads as "less than a day" rather than a negative count: the item <b>is</b> late — the
    /// monitor said so — and only the elapsed time is uncertain.
    /// </remarks>
    public static string Elapsed(DateTime overdueSince, DateTime nowUtc)
    {
        var days = ElapsedDays(overdueSince, nowUtc);

        return days switch
        {
            0 => "há menos de 1 dia",
            1 => "há 1 dia",
            _ => $"há {days} dias"
        };
    }

    /// <summary>
    /// Tint for the delay cell — the only coloured cell in the overdue tables. Colour is spent on severity
    /// alone (design system: one signal per meaning): orange while the delay is recent, red from
    /// <see cref="CriticalDays"/> days on. Status and priority keep their shared labels.
    /// </summary>
    public static string SeverityClass(DateTime overdueSince, DateTime nowUtc) =>
        ElapsedDays(overdueSince, nowUtc) >= CriticalDays
            ? "text-[#ef4444]"
            : "text-[#ff7a3d]";

    /// <summary>Whole days elapsed, floored at zero so clock skew never produces a negative delay.</summary>
    private static int ElapsedDays(DateTime overdueSince, DateTime nowUtc)
    {
        var elapsed = nowUtc - overdueSince;

        return elapsed <= TimeSpan.Zero ? 0 : (int)elapsed.TotalDays;
    }
}
