using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Application.UseCases.Categories;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Contracts.Common;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Thin typed client the Blazor briefing screens use to reach the REST API (BRF-013) — keeps HTTP concerns
/// out of the Razor components (CLAUDE.md: components stay thin). Runs over the <c>"Api"</c> HttpClient, so
/// bearer-token attachment and refresh-on-401 come for free. Write operations surface the API's
/// validation/business failures as a failed <see cref="Result{T}"/> whose <c>Errors</c> are keyed by field.
/// </summary>
public interface IBriefingsApiClient
{
    /// <summary>
    /// Fetches a single briefing for the edit form, with its attachments (RF005). A 404/403 surfaces as the
    /// matching failed <see cref="Result{T}"/>.
    /// </summary>
    Task<Result<BriefingDetailDto>> GetBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the authenticated requester's own briefings via <c>GET /briefings</c> (BRF-014, RF008),
    /// paginated and optionally filtered by status/category.
    /// </summary>
    Task<Result<PagedResponse<BriefingDto>>> GetBriefingsAsync(BriefingListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the active categories for the create picker. Degrades gracefully: any failure returns an empty
    /// list so the form can render its "categories unavailable" state rather than throwing.
    /// </summary>
    Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a draft briefing via <c>POST /briefings</c>. Field-level failures land in <c>Result.Errors</c>.</summary>
    Task<Result<BriefingDto>> CreateBriefingAsync(CreateBriefingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a briefing via <c>PUT /briefings/{id}</c>. Field-level failures land in <c>Result.Errors</c>.</summary>
    Task<Result<BriefingDto>> UpdateBriefingAsync(Guid id, UpdateBriefingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Submits a briefing for approval via <c>POST /briefings/{id}/submit</c> (RF007).</summary>
    Task<Result<BriefingDto>> SubmitBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a briefing via <c>POST /briefings/{id}/cancel</c> (RF010). A status that no longer allows
    /// cancellation comes back as a <see cref="ErrorType.Conflict"/> failure.
    /// </summary>
    Task<Result<BriefingDto>> CancelBriefingAsync(Guid id, CancellationToken cancellationToken = default);
}
