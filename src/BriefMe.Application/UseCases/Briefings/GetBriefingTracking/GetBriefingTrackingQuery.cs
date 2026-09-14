using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingTracking;

/// <summary>
/// Reads a briefing's progress and the tasks behind it (BRF-039, RF023) — the single read the tracking
/// screen is built from. The caller comes from <c>ICurrentUser</c>: the briefing's requester or a
/// management profile may track it, anyone else gets <c>Forbidden</c>.
/// </summary>
public sealed record GetBriefingTrackingQuery(Guid BriefingId) : IQuery<BriefingTrackingDto>;
