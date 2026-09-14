using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Briefings;

/// <summary>
/// Payload for <c>PUT /briefings/{id}</c> (BRF-012). The target id comes from the route; the editor is the
/// authenticated user (<c>ICurrentUser</c>). The category is fixed at creation and cannot be changed here.
/// </summary>
public sealed record UpdateBriefingRequest(
    string Title,
    Priority Priority,
    string? Description = null,
    string? Objective = null,
    string? RequestingArea = null,
    DateOnly? DesiredDeliveryDate = null);
