namespace BriefMe.Domain.ValueObjects;

/// <summary>
/// How far a briefing has come: how many of its tasks are approved, out of how many (RF023, RF059). Produced
/// by <see cref="Services.BriefingProgressCalculator"/> (BRF-035) and never stored — the value is derived from
/// the tasks that exist at the moment it is asked for.
/// </summary>
/// <remarks>
/// Carries the two counters as well as the percentage because the tracking screen (RF023, BRF-039) shows
/// "2 of 4 tasks (50%)": returning a bare number would make every consumer count the list again.
/// </remarks>
public sealed record BriefingProgress
{
    /// <summary>Percentage is a display value; two decimals is what a progress bar or a label can show.</summary>
    private const int PercentageDecimals = 2;

    /// <summary>
    /// Largest value below 100 at <see cref="PercentageDecimals"/>, and the smallest above 0. They exist so a
    /// briefing that is nearly — but not — finished never reads as 100%, and one that has barely started
    /// never reads as 0%.
    /// </summary>
    private const decimal HighestIncompletePercentage = 99.99m;

    private const decimal LowestStartedPercentage = 0.01m;

    private BriefingProgress(int totalTasks, int approvedTasks, decimal percentage)
    {
        TotalTasks = totalTasks;
        ApprovedTasks = approvedTasks;
        Percentage = percentage;
    }

    /// <summary>Every task of the briefing (RF016), whatever its status — the denominator.</summary>
    public int TotalTasks { get; }

    /// <summary>
    /// Tasks in <see cref="Enums.BriefingTaskStatus.Approved"/> — the numerator. Only final approval counts:
    /// a task awaiting review or returned for correction (RF048) is work still owed.
    /// </summary>
    public int ApprovedTasks { get; }

    /// <summary>
    /// <see cref="ApprovedTasks"/> over <see cref="TotalTasks"/>, from 0 to 100, rounded to two decimals for
    /// display. It reaches 100 only when <see cref="IsComplete"/>, and 0 only when nothing is approved yet.
    /// </summary>
    public decimal Percentage { get; }

    /// <summary>
    /// Whether every task of the briefing is approved (RF058) — the condition the automatic completion rule
    /// (RF024, BRF-036) reacts to. Answered by counting, never by comparing <see cref="Percentage"/> with 100:
    /// a rounded decimal is a display value, not a business decision. A briefing with no tasks is not complete.
    /// </summary>
    public bool IsComplete => TotalTasks > 0 && ApprovedTasks == TotalTasks;

    /// <summary>
    /// Builds the progress from the two counters, deriving <see cref="Percentage"/>.
    /// </summary>
    /// <remarks>
    /// A briefing with no tasks is 0%, not an undefined value: it is the ordinary state of a briefing that has
    /// been approved but not broken down yet (BRF-026), and pushing a null case into every screen to describe
    /// it would buy nothing — <see cref="IsComplete"/> already keeps 0-of-0 from reading as "done".
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A count is negative, or more tasks are approved than exist. Both are caller programming errors, not
    /// business states, so they surface as argument exceptions rather than as a domain rule.
    /// </exception>
    public static BriefingProgress FromCounts(int totalTasks, int approvedTasks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalTasks);
        ArgumentOutOfRangeException.ThrowIfNegative(approvedTasks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(approvedTasks, totalTasks);

        return new BriefingProgress(totalTasks, approvedTasks, CalculatePercentage(totalTasks, approvedTasks));
    }

    /// <summary>
    /// The ratio as a percentage, in <c>decimal</c> rather than <c>double</c> — the value goes to a screen and
    /// is compared, so binary floating point error has nothing to offer here.
    /// </summary>
    private static decimal CalculatePercentage(int totalTasks, int approvedTasks)
    {
        if (totalTasks == 0 || approvedTasks == 0)
            return 0m;

        if (approvedTasks == totalTasks)
            return 100m;

        var rounded = Math.Round(
            approvedTasks * 100m / totalTasks,
            PercentageDecimals,
            MidpointRounding.AwayFromZero);

        // 199 of 200 rounds to 100, and 1 of 10000 rounds to 0. Both are lies about a briefing that is
        // neither finished nor untouched, so the value stops just short of each end.
        return Math.Clamp(rounded, LowestStartedPercentage, HighestIncompletePercentage);
    }
}
