using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Tasks;

/// <summary>
/// Payload for <c>PUT /tasks/{id}/deadline-priority</c> (BRF-032, RF018 + RF019). The two travel together
/// because planning a task is one act of the Design Manager — the same reason
/// <c>SetDeadlinePriorityCommand</c> carries both.
/// </summary>
/// <remarks>
/// The "not in the past" rule is not restated as a data annotation: it belongs to
/// <c>SetDeadlinePriorityCommandValidator</c>, which is the single place that decides it and is re-applied
/// by the aggregate.
/// </remarks>
public sealed record SetDeadlinePriorityRequest(DateOnly Deadline, Priority Priority);
