using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Entities;

/// <summary>
/// The agreed turnaround for a kind of demand (RF027, BRF-040/041): "a demand of this category, with this
/// priority, has N hours". A rule aimed at nothing in particular — no category, no priority — is the house
/// default that catches everything else.
/// </summary>
/// <remarks>
/// Policy, not work: a rule holds no reference to the briefings or tasks it governs, and they hold none to
/// it. Binding a demand to the rule that applied at the time would mean rewriting every open row whenever an
/// administrator changes the default; instead the applicable rule is resolved when the deadline monitor runs
/// (RF028, BRF-043), by matching category and priority through <see cref="AppliesTo"/>.
/// <para>
/// Rules are written by the Administrator-only configuration use case (RF027, BRF-042), which identifies a
/// rule by its scope: it creates the rule when the scope is free and calls <see cref="ChangeDeadline"/> when
/// it is already taken.
/// </para>
/// </remarks>
public class SlaRule : AggregateRoot, IAuditableEntity
{
    private SlaRule(Guid id, Guid? categoryId, Priority? priority, SlaDeadline deadline) : base(id)
    {
        CategoryId = categoryId;
        Priority = priority;
        Deadline = deadline;
    }

    private SlaRule()
    {
        Deadline = null!;
    }

    /// <summary>
    /// Category the rule is aimed at (RF040), or <c>null</c> for any category. Logical FK; the physical
    /// constraint arrives with BRF-059, exactly as with <see cref="Briefing.CategoryId"/>.
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Priority the rule is aimed at (RF019), or <c>null</c> for any priority.</summary>
    public Priority? Priority { get; private set; }

    /// <summary>How long a matching demand may take (RF027). Persisted as the <c>DeadlineHours</c> column.</summary>
    public SlaDeadline Deadline { get; private set; }

    /// <summary>
    /// How narrowly this rule is aimed, derived from the two nullable columns above. Ordered, so the most
    /// specific rule among those matching a demand is a single, deterministic answer (BRF-043).
    /// </summary>
    public SlaRuleScope Scope => (CategoryId, Priority) switch
    {
        (not null, not null) => SlaRuleScope.CategoryAndPriority,
        (not null, null) => SlaRuleScope.Category,
        (null, not null) => SlaRuleScope.Priority,
        _ => SlaRuleScope.Global
    };

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Creates an SLA rule (RF027). Both scope sides are optional: leaving them out widens the rule, and
    /// leaving out both makes it the global fallback.
    /// </summary>
    /// <remarks>
    /// That no other rule already covers the same scope is enforced by the unique index behind the table —
    /// the aggregate cannot see its siblings. Translating that collision into a readable business failure is
    /// the configuration use case's job (BRF-042).
    /// </remarks>
    public static SlaRule Create(Guid? categoryId, Priority? priority, int deadlineHours)
    {
        if (priority is not null && !Enum.IsDefined(priority.Value))
            throw new DomainException($"Unknown SLA rule priority: {(int)priority.Value}.");

        return new SlaRule(
            Guid.NewGuid(),
            // An empty id is no category at all, not a category nothing can match — same normalization
            // BriefingTask.Create applies to its designer.
            categoryId == Guid.Empty ? null : categoryId,
            priority,
            ParseDeadline(deadlineHours));
    }

    /// <summary>
    /// Changes how long a matching demand may take (RF027, BRF-042). Only the deadline moves: the scope is
    /// this rule's identity — the very pair the unique index protects — so retargeting it would mean writing
    /// a different rule, not editing this one.
    /// </summary>
    /// <remarks>
    /// Reconfiguring never rewrites the demands already open. The rule that governs a demand is resolved when
    /// the deadline monitor runs (BRF-043), so a new deadline simply applies from the next pass on.
    /// </remarks>
    public void ChangeDeadline(int deadlineHours) => Deadline = ParseDeadline(deadlineHours);

    /// <summary>
    /// Turns the value object's expected failure into an exception, since the caller of an aggregate method
    /// has already had its chance to validate — same pairing as <c>Briefing.Reject</c>/<c>Justification</c>.
    /// The configuration use case (BRF-042) checks the value object first and answers with a
    /// <c>Result</c> failure, so user input never reaches this line.
    /// </summary>
    private static SlaDeadline ParseDeadline(int deadlineHours)
    {
        var deadline = SlaDeadline.Create(deadlineHours);

        if (deadline.IsFailure)
            throw new DomainException(deadline.Error ?? "SLA deadline is invalid.");

        return deadline.Value;
    }

    /// <summary>
    /// Whether this rule governs a demand of the given category and priority (RF028). Each filled side of the
    /// scope must match; an empty side is a wildcard, which is what makes the global rule match everything.
    /// </summary>
    /// <remarks>
    /// A rule only speaks for itself. Choosing between several matching rules is a domain service of BRF-043
    /// (<c>rules.Where(r => r.AppliesTo(c, p)).MaxBy(r => r.Scope)</c>) — reaching for the other rules from
    /// here would be the same coupling RF057 keeps out of tasks.
    /// </remarks>
    public bool AppliesTo(Guid categoryId, Priority priority) =>
        (CategoryId is null || CategoryId == categoryId) &&
        (Priority is null || Priority == priority);
}
