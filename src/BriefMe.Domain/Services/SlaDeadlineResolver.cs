using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Services;

/// <summary>
/// Answers "when is this demand due?" (RF028, BRF-043) — the single definition the deadline monitor compares
/// against the clock. Two steps: pick the SLA rule that governs the demand among the configured ones
/// (RF027), then combine the deadline that rule implies with the deadline the demand carries of its own
/// (RF018 on a task, <see cref="Briefing.DesiredDeliveryDate"/> on a briefing).
/// </summary>
/// <remarks>
/// A domain service rather than a method on <see cref="SlaRule"/>: a rule only speaks for itself
/// (<see cref="SlaRule.AppliesTo"/>, <see cref="SlaRule.Scope"/>), and choosing among candidates would mean
/// letting one rule see its siblings — the coupling BRF-040 deliberately kept out. Same reasoning, and same
/// registration, as <see cref="BriefingProgressCalculator"/>.
/// <para>
/// Pure: no repository, no clock, no injected service. The caller loads the policy table and passes the
/// demand's own dates in; the same inputs always yield the same instant.
/// </para>
/// </remarks>
public sealed class SlaDeadlineResolver
{
    /// <summary>
    /// The rule that governs a demand of this category and priority: the most specific one among those that
    /// match, or <c>null</c> when none does — an empty policy table, or a table with no global fallback.
    /// </summary>
    /// <remarks>
    /// A tie is impossible by construction. Two rules of the same <see cref="SlaRuleScope"/> matching the
    /// same demand would have to carry the same <c>(CategoryId, Priority)</c> pair, which the unique index
    /// <c>UX_SlaRules_CategoryId_Priority</c> forbids. That is why the enum is ordered (BRF-040, decision #3)
    /// rather than a count of filled sides, which would tie a category rule with a priority one.
    /// </remarks>
    public SlaRule? ResolveRule(IReadOnlyCollection<SlaRule> rules, Guid categoryId, Priority priority)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return rules
            .Where(rule => rule.AppliesTo(categoryId, priority))
            .MaxBy(rule => rule.Scope);
    }

    /// <summary>
    /// The instant a demand is due: the <b>earliest</b> of the deadline typed onto it and the one its SLA
    /// rule implies. <c>null</c> when it carries neither — a demand with no commitment of any kind is never
    /// late.
    /// </summary>
    /// <param name="rules">The whole SLA policy (RF027). An empty collection means no SLA-derived deadline.</param>
    /// <param name="categoryId">Category the demand belongs to (RF040) — a task borrows its briefing's.</param>
    /// <param name="priority">Priority of the demand itself; a task's (RF019) may differ from its briefing's.</param>
    /// <param name="slaStartedAtUtc">
    /// When the design sector took the demand on, which is where the SLA clock starts: a task's creation
    /// (it can only be created for an approved briefing, RF015) or a briefing's approval
    /// (<see cref="Briefing.ReviewedAt"/>). Null before that instant exists, which suppresses the
    /// SLA-derived deadline without suppressing the typed one.
    /// </param>
    /// <param name="explicitDeadline">The date typed onto the demand, if any.</param>
    /// <remarks>
    /// Earliest wins because a demand carries two promises — the specific one and the house's (RF027) — and
    /// it is late the moment either breaks. Letting the typed date win instead would make the SLA a
    /// suggestion: a generous date on an urgent demand would keep it off the overdue list for good, which is
    /// the opposite of what RF028 monitors for.
    /// </remarks>
    public DateTime? ResolveDueAt(
        IReadOnlyCollection<SlaRule> rules,
        Guid categoryId,
        Priority priority,
        DateTime? slaStartedAtUtc,
        DateOnly? explicitDeadline)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var slaDueAt = slaStartedAtUtc is null
            ? null
            : ResolveRule(rules, categoryId, priority)?.Deadline.DueAtFrom(slaStartedAtUtc.Value);

        var explicitDueAt = explicitDeadline is null
            ? (DateTime?)null
            : EndOfDayUtc(explicitDeadline.Value);

        return (slaDueAt, explicitDueAt) switch
        {
            (null, null) => null,
            (not null, null) => slaDueAt,
            (null, not null) => explicitDueAt,
            _ => slaDueAt < explicitDueAt ? slaDueAt : explicitDueAt
        };
    }

    /// <summary>
    /// The instant a calendar date stops being "today": midnight UTC at the start of the following day. A
    /// deadline is a date, so it runs to the end of that day — the same reading
    /// <see cref="BriefingTask.SetDeadlineAndPriority"/> already applies when it accepts today as a future
    /// date. Without it, a task planned for today would be late on the monitor's first morning sweep.
    /// </summary>
    public static DateTime EndOfDayUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
}
