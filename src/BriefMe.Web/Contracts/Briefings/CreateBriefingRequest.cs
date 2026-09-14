using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Briefings;

/// <summary>
/// Payload for <c>POST /briefings</c> (BRF-012). The requester is the authenticated user
/// (<c>ICurrentUser</c>) and is not part of the request.
/// </summary>
public sealed record CreateBriefingRequest(
    string Title,
    Priority Priority,
    Guid CategoryId,
    string? Description = null,
    string? Objective = null,
    string? RequestingArea = null,
    DateOnly? DesiredDeliveryDate = null);
