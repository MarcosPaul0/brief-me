using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

/// <summary>
/// Access to the SLA policy (RF027). Defined in <c>Domain</c> per the project's Repository pattern, and
/// introduced by the use case that first needs it (BRF-042), as <see cref="IBriefingTaskRepository"/> was by
/// BRF-025.
/// </summary>
public interface ISlaRuleRepository : IRepository<SlaRule>
{
    /// <summary>
    /// The rule aimed at exactly this scope, or <c>null</c> when the scope has no rule yet. A <c>null</c>
    /// side means "any" and is matched literally: <c>(null, null)</c> finds the global rule, never a
    /// category- or priority-scoped one.
    /// </summary>
    /// <remarks>
    /// This is an identity lookup, not the resolution of "which rule governs this demand" — that one takes
    /// the most specific rule among those that match (<c>SlaRule.AppliesTo</c> + <c>Scope</c>) and belongs to
    /// the deadline monitor of BRF-043. Here the scope <b>is</b> the key, the same pair the unique index
    /// <c>UX_SlaRules_CategoryId_Priority</c> protects.
    /// </remarks>
    Task<SlaRule?> GetByScopeAsync(
        Guid? categoryId,
        Priority? priority,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The whole policy, in one read. The deadline monitor (RF028, BRF-043) resolves the applicable rule for
    /// every demand it sweeps, and the winner is the most specific among <b>all</b> matches — a question no
    /// per-demand query can answer without loading the same handful of rows again and again.
    /// </summary>
    /// <remarks>
    /// Unpaginated on purpose, and safely so: the table holds at most one rule per
    /// <c>(category, priority)</c> pair, so its size is bounded by the category catalog times four
    /// priorities — dozens of rows, written only by an administrator (RF027).
    /// </remarks>
    Task<IReadOnlyList<SlaRule>> GetAllAsync(CancellationToken cancellationToken = default);
}
