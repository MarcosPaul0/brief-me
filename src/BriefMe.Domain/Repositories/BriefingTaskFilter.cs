using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

/// <summary>Optional filters for querying a designer's tasks (BRF-025/BRF-033).</summary>
public sealed record BriefingTaskFilter(BriefingTaskStatus? Status = null, Priority? Priority = null);
