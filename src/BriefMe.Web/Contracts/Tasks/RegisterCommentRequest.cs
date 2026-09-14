namespace BriefMe.Web.Contracts.Tasks;

/// <summary>
/// Payload for <c>POST /tasks/{id}/comments</c> (BRF-032, RF021). The task comes from the route and the
/// author is the authenticated user (<c>ICurrentUser</c>) — carrying an author id here would let one
/// involved person comment as another.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is nullable because that is what an absent JSON field deserializes to; forwarding it
/// as-is lets <c>RegisterCommentCommandValidator</c> answer with the catalog's message instead of the
/// framework's binding error.
/// </remarks>
public sealed record RegisterCommentRequest(string? Text);
