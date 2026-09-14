using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Tasks;

/// <summary>
/// Payload for <c>PUT /tasks/{id}/status</c> (BRF-032, RF020). The task comes from the route and the caller
/// is the authenticated user (<c>ICurrentUser</c>), whose right to move <b>this</b> task is decided by
/// <c>UpdateTaskStatusCommandHandler</c>.
/// </summary>
/// <remarks>
/// Accepts the whole <see cref="BriefingTaskStatus"/> enum. The four approval states of RF042–RF048 are
/// legitimate values that this door does not open: the handler answers them with a <c>409</c> naming the
/// action to use instead, which is more useful than a binding error.
/// </remarks>
public sealed record UpdateTaskStatusRequest(BriefingTaskStatus Status);
