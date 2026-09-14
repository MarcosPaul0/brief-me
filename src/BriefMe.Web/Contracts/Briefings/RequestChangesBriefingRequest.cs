namespace BriefMe.Web.Contracts.Briefings;

/// <summary>
/// Payload for <c>POST /briefings/{id}/request-changes</c> (BRF-019, RF014). Same shape as
/// <see cref="RejectBriefingRequest"/> — the reviewer describes what must change — but the briefing stays
/// alive and editable instead of ending in <c>Rejected</c>. Kept as its own type so the two endpoints can
/// evolve independently.
/// </summary>
public sealed record RequestChangesBriefingRequest(string Justification);
