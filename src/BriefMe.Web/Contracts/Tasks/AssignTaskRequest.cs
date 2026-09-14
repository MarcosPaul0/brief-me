namespace BriefMe.Web.Contracts.Tasks;

/// <summary>
/// Payload for <c>PUT /tasks/{id}/assign</c> (BRF-032, RF017). The task comes from the route and the
/// assigner is the authenticated user (<c>ICurrentUser</c>), so the body carries only the target designer.
/// Whether that user really is an active Designer is checked by <c>AssignTaskCommandHandler</c>, which needs
/// the role catalog to decide.
/// </summary>
public sealed record AssignTaskRequest(Guid DesignerId);
