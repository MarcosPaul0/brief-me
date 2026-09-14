using BriefMe.Domain.Enums;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Parameters of the requester's briefing listing (BRF-014, RF008). Mirrors
/// <c>BriefingsController.GetMyBriefings</c>'s query string; the requester itself is never sent — the API
/// scopes the result to the authenticated user.
/// </summary>
public sealed record BriefingListQuery(
    int Page,
    int PageSize,
    BriefingStatus? Status = null,
    Guid? CategoryId = null);
