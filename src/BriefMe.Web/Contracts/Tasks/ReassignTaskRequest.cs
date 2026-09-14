namespace BriefMe.Web.Contracts.Tasks;

/// <summary>
/// Payload for <c>PUT /tasks/{id}/reassign</c> (BRF-032, RF026). The task comes from the route and the caller
/// is the authenticated user (<c>ICurrentUser</c>), so the body carries only the Designer taking the work
/// over. The one being replaced is not sent: it is whoever the task currently holds, and
/// <c>ReassignTaskCommandHandler</c> reads it from the database.
/// </summary>
public sealed record ReassignTaskRequest(Guid DesignerId);
