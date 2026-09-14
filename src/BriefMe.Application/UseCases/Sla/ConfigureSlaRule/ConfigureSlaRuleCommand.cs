using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Sla.ConfigureSlaRule;

/// <summary>
/// Declares how long a demand of a given scope may take (RF027, BRF-042): "a briefing of this category, with
/// this priority, has N hours". Leaving <see cref="CategoryId"/> or <see cref="Priority"/> out widens the
/// rule; leaving both out configures the global fallback.
/// </summary>
/// <remarks>
/// The scope is the rule's identity — the pair the unique index protects — so the command carries no rule id:
/// configuring a scope that already has a rule adjusts that rule instead of adding a second one. The
/// administrator behind the act is enforced by the <c>sla.configure</c> policy on the endpoint, not carried
/// here: no SLA rule belongs to a user.
/// </remarks>
public sealed record ConfigureSlaRuleCommand(
    Guid? CategoryId,
    Priority? Priority,
    int DeadlineHours) : ICommand<SlaRuleDto>;
