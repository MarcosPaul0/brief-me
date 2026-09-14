namespace BriefMe.Domain.Enums;

/// <summary>
/// How narrowly an SLA rule is aimed (RF027). Not a persisted column — it is derived from the two nullable
/// scope columns of the rule.
/// </summary>
/// <remarks>
/// The numeric order <b>is</b> the specificity order: the most specific rule among those that match a demand
/// wins (BRF-043). Ordering them totally is the point — counting the filled sides instead would tie a
/// category-only rule with a priority-only one, and the winner would depend on the order the database
/// happened to return the rows. Category outranks priority because a category is the kind of work ("a video
/// takes longer than a banner"), configured by the design sector, while priority is urgency declared by the
/// requester.
/// </remarks>
public enum SlaRuleScope
{
    /// <summary>No category, no priority: the house default, the fallback when nothing narrower matches.</summary>
    Global = 0,

    Priority = 1,

    Category = 2,

    CategoryAndPriority = 3
}
