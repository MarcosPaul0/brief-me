using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

/// <summary>Optional filters for querying a requester's briefings (BRF-005/BRF-008).</summary>
public sealed record BriefingFilter(BriefingStatus? Status = null, Guid? CategoryId = null);
