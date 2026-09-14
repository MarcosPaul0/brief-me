using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Tasks;

/// <summary>
/// Payload for <c>POST /briefings/{id}/tasks</c> (BRF-032, RF015). The parent briefing comes from the route
/// and the author is the authenticated user (<c>ICurrentUser</c>), so the body carries only the batch.
/// </summary>
/// <remarks>
/// A wrapper object rather than a bare JSON array: RF015 is one act that may later need batch-level data
/// (a note, a template id), and an object can grow a field without breaking every client.
/// </remarks>
public sealed record CreateBriefingTasksRequest(IReadOnlyList<CreateBriefingTaskItemRequest> Tasks);

/// <summary>
/// One task of the batch. Mirrors <c>CreateBriefingTaskItem</c>: the assignee (RF017) and the deadline
/// (RF018) arrive through their own endpoints, which own the side effects those changes carry.
/// </summary>
public sealed record CreateBriefingTaskItemRequest(string Title, Priority Priority);
