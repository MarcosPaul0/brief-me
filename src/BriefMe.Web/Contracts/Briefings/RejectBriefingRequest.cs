namespace BriefMe.Web.Contracts.Briefings;

/// <summary>
/// Payload for <c>POST /briefings/{id}/reject</c> (BRF-019, RF012). The target id comes from the route and
/// the reviewer is the authenticated user (<c>ICurrentUser</c>), so the body carries only the mandatory
/// justification (RF013). The justification rule itself is not restated as a data annotation: it belongs to
/// the <c>Justification</c> value object and is re-applied by <c>RejectBriefingCommandValidator</c>.
/// </summary>
public sealed record RejectBriefingRequest(string Justification);
