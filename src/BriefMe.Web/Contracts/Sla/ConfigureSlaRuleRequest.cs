using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Sla;

/// <summary>
/// Payload for <c>PUT /sla-rules</c> (BRF-042, RF027). The scope travels in the body because it — not a row
/// id — identifies the rule being configured: omitting <see cref="CategoryId"/> or <see cref="Priority"/>
/// widens the rule, and omitting both configures the global fallback.
/// </summary>
/// <remarks>
/// The accepted deadline range is not restated as a data annotation: it belongs to <c>SlaDeadline</c>, which
/// <c>ConfigureSlaRuleCommandValidator</c> and the aggregate both defer to.
/// </remarks>
public sealed record ConfigureSlaRuleRequest(Guid? CategoryId, Priority? Priority, int DeadlineHours);
