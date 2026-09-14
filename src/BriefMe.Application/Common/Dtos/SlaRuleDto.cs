using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Output representation of a configured SLA rule (RF027, BRF-042). <c>CategoryId</c> and <c>Priority</c> are
/// nullable because a null side is a wildcard — "any category", "any priority" — and both null is the global
/// fallback rule.
/// </summary>
/// <remarks>
/// <c>Scope</c> is derived from the two nullable sides rather than stored, and is echoed back because it is
/// what confirms the act to the administrator: whether the deadline just configured landed on the house
/// default or on a narrower rule.
/// </remarks>
public sealed record SlaRuleDto(
    Guid Id,
    Guid? CategoryId,
    Priority? Priority,
    int DeadlineHours,
    SlaRuleScope Scope)
{
    public static SlaRuleDto FromSlaRule(SlaRule slaRule)
    {
        return new(
            slaRule.Id,
            slaRule.CategoryId,
            slaRule.Priority,
            slaRule.Deadline.Hours,
            slaRule.Scope);
    }
}
