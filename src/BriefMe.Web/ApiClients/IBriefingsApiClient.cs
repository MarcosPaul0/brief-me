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

    /// <summary>
    /// Attaches a reference file via <c>POST /briefings/{id}/attachments</c> as <c>multipart/form-data</c>
    /// (RF005, BRF-013). Takes the bytes already read from the picked file: the request may be replayed after
    /// a token refresh, so the body has to be reproducible. Size and extension limits are the server's
    /// (<c>IFileValidator</c>, BRF-011) and surface as validation errors.
    /// </summary>
    Task<Result<AttachmentDto>> UploadAttachmentAsync(
        Guid briefingId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one attachment via <c>DELETE /briefings/{id}/attachments/{attachmentId}</c> and returns the
    /// briefing with the files that remain, so the caller does not re-fetch.
    /// </summary>
    Task<Result<BriefingDetailDto>> RemoveAttachmentAsync(
        Guid briefingId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    /// <summary>Submits a briefing for approval via <c>POST /briefings/{id}/submit</c> (RF007).</summary>
    Task<Result<BriefingDto>> SubmitBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a briefing via <c>POST /briefings/{id}/cancel</c> (RF010). A status that no longer allows
    /// cancellation comes back as a <see cref="ErrorType.Conflict"/> failure.
    /// </summary>
    Task<Result<BriefingDto>> CancelBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a briefing's progress and task timeline via <c>GET /briefings/{id}/tracking</c> (BRF-039,
    /// RF023). The percentage comes computed from the backend (BRF-035) — the screen never counts tasks.
    /// A caller who is neither the requester nor design management comes back as a
    /// <see cref="ErrorType.Forbidden"/> failure.
    /// </summary>
    Task<Result<BriefingTrackingDto>> GetBriefingTrackingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the briefings awaiting review via <c>GET /briefings/pending-approval</c> (BRF-020, RF011),
    /// paginated. Feeds the Design Manager's approval panel; requires <c>briefings.view-all</c>.
    /// </summary>
    Task<Result<PagedResponse<PendingApprovalBriefingDto>>> GetPendingApprovalBriefingsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the briefings past their due instant via <c>GET /briefings/overdue</c> (BRF-046, RF030),
    /// paginated, longest late first — the demand half of the SLA dashboard. Requires
    /// <c>briefings.view-all</c>: the list crosses every requester's demands, so <c>briefings.view-own</c>
    /// comes back as <see cref="ErrorType.Forbidden"/>. Takes no filter — the query has none, by BRF-045's
    /// decision.
    /// </summary>
    Task<Result<PagedResponse<OverdueBriefingDto>>> GetOverdueBriefingsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a briefing under review via <c>POST /briefings/{id}/approve</c> (RF011). A briefing that
    /// already left <c>AwaitingApproval</c> comes back as a <see cref="ErrorType.Conflict"/> failure.
    /// </summary>
    Task<Result<BriefingDto>> ApproveBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a briefing via <c>POST /briefings/{id}/reject</c> (RF012). The justification is mandatory
    /// (RF013); a blank one surfaces as a validation failure keyed by <c>Justification</c>.
    /// </summary>
    Task<Result<BriefingDto>> RejectBriefingAsync(Guid id, string justification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a briefing to its requester via <c>POST /briefings/{id}/request-changes</c> (RF014), keeping
    /// it editable. Same mandatory justification as <see cref="RejectBriefingAsync"/>.
    /// </summary>
    Task<Result<BriefingDto>> RequestChangesBriefingAsync(Guid id, string justification, CancellationToken cancellationToken = default);
}
