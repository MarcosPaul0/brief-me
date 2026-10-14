using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Contracts.Common;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Thin typed client the Blazor briefing screens use to reach the REST API — keeps HTTP concerns out of the
/// Razor components. Failures come back as a failed <see cref="Result{T}"/>; validation failures carry their
/// field-keyed <c>Errors</c>.
/// </summary>
public interface IBriefingsApiClient
{
    /// <summary>A single briefing with display names. 403/404 surface as the matching failure type.</summary>
    Task<Result<BriefingDetailDto>> GetBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The requester's own briefings, paginated and optionally filtered.</summary>
    Task<Result<PagedResponse<BriefingListItemDto>>> GetBriefingsAsync(BriefingListQuery query, CancellationToken cancellationToken = default);

    /// <summary>The designer queue: submitted, unclaimed briefings, oldest first.</summary>
    Task<Result<PagedResponse<BriefingListItemDto>>> GetQueueAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The briefings the calling designer claimed, optionally filtered by status.</summary>
    Task<Result<PagedResponse<BriefingListItemDto>>> GetAssignedAsync(
        int page,
        int pageSize,
        BriefingStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The active categories for the create picker. Degrades to an empty list on any failure so the form can
    /// render its "categories unavailable" state.
    /// </summary>
    Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> CreateBriefingAsync(CreateBriefingRequest request, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> UpdateBriefingAsync(Guid id, UpdateBriefingRequest request, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> SubmitBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> CancelBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> AcceptDeliveryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> RequestChangesAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> ClaimBriefingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BriefingDto>> DeliverBriefingAsync(Guid id, string deliveryNote, CancellationToken cancellationToken = default);
}
