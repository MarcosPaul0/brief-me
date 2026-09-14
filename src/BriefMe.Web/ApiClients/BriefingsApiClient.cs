using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Application.UseCases.Categories;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Contracts.Common;
using BriefMe.Web.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Typed client for the briefing create/edit form (BRF-013). Sends through <see cref="AuthenticatedApiClient"/>
/// (the "Api" pipeline: bearer + refresh-on-401) and maps non-success responses via
/// <see cref="ApiProblemMapper"/>, so screens surface backend failures uniformly.
/// </summary>
public sealed class BriefingsApiClient : IBriefingsApiClient
{
    private const string LoadBriefingFailureMessage = "Não foi possível carregar o briefing. Tente novamente.";
    private const string LoadBriefingsFailureMessage = "Não foi possível carregar os briefings. Tente novamente.";
    private const string CancelBriefingFailureMessage = "Não foi possível cancelar o briefing. Tente novamente.";
    private const string LoadTrackingFailureMessage = "Não foi possível carregar o acompanhamento do briefing. Tente novamente.";
    private const string SaveBriefingFailureMessage = "Não foi possível salvar o briefing. Tente novamente.";
    private const string SubmitBriefingFailureMessage = "Não foi possível enviar o briefing para aprovação. Tente novamente.";
    private const string LoadPendingBriefingsFailureMessage = "Não foi possível carregar os briefings pendentes de aprovação. Tente novamente.";
    private const string LoadOverdueBriefingsFailureMessage = "Não foi possível carregar os briefings atrasados. Tente novamente.";
    private const string ApproveBriefingFailureMessage = "Não foi possível aprovar o briefing. Tente novamente.";
    private const string RejectBriefingFailureMessage = "Não foi possível rejeitar o briefing. Tente novamente.";
    private const string RequestChangesBriefingFailureMessage = "Não foi possível solicitar ajustes no briefing. Tente novamente.";
    private const string UploadAttachmentFailureMessage = "Não foi possível anexar o arquivo. Tente novamente.";
    private const string RemoveAttachmentFailureMessage = "Não foi possível remover o anexo. Tente novamente.";

    private static readonly JsonSerializerOptions ResponseJsonOptions = ApiProblemMapper.JsonOptions;

    private readonly AuthenticatedApiClient _api;

    public BriefingsApiClient(AuthenticatedApiClient api) => _api = api;

    public async Task<Result<BriefingDetailDto>> GetBriefingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync($"briefings/{id}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<BriefingDetailDto>(response, LoadBriefingFailureMessage, cancellationToken: cancellationToken);

            var briefing = await response.Content.ReadFromJsonAsync<BriefingDetailDto>(ResponseJsonOptions, cancellationToken);
            return briefing is null
                ? Result.Failure<BriefingDetailDto>(LoadBriefingFailureMessage)
                : Result.Success(briefing);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDetailDto>(LoadBriefingFailureMessage);
        }
    }

    public async Task<Result<AttachmentDto>> UploadAttachmentAsync(
        Guid briefingId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Rebuilt per attempt: the 401 retry sends a second request, and a consumed HttpContent cannot
            // be sent twice. The part is named "file" to bind to the controller's IFormFile parameter.
            using var response = await _api.PostAsync(
                $"briefings/{briefingId}/attachments",
                () => new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<AttachmentDto>(response, UploadAttachmentFailureMessage, cancellationToken: cancellationToken);

            var attachment = await response.Content.ReadFromJsonAsync<AttachmentDto>(ResponseJsonOptions, cancellationToken);

            return attachment is null
                ? Result.Failure<AttachmentDto>(UploadAttachmentFailureMessage)
                : Result.Success(attachment);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<AttachmentDto>(UploadAttachmentFailureMessage);
        }
    }

    public async Task<Result<BriefingDetailDto>> RemoveAttachmentAsync(
        Guid briefingId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.DeleteAsync($"briefings/{briefingId}/attachments/{attachmentId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<BriefingDetailDto>(response, RemoveAttachmentFailureMessage, cancellationToken: cancellationToken);

            var briefing = await response.Content.ReadFromJsonAsync<BriefingDetailDto>(ResponseJsonOptions, cancellationToken);

            return briefing is null
                ? Result.Failure<BriefingDetailDto>(RemoveAttachmentFailureMessage)
                : Result.Success(briefing);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDetailDto>(RemoveAttachmentFailureMessage);
        }
    }

    public async Task<Result<PagedResponse<BriefingDto>>> GetBriefingsAsync(BriefingListQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync(BuildBriefingsRequestUri(query), cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<PagedResponse<BriefingDto>>(response, LoadBriefingsFailureMessage, cancellationToken: cancellationToken);

            var page = await response.Content.ReadFromJsonAsync<PagedResponse<BriefingDto>>(ResponseJsonOptions, cancellationToken);

            return page is null
                ? Result.Failure<PagedResponse<BriefingDto>>(LoadBriefingsFailureMessage)
                : Result.Success(page);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<BriefingDto>>(LoadBriefingsFailureMessage);
        }
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync("categories", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return [];

            var categories = await response.Content.ReadFromJsonAsync<List<CategoryListItemDto>>(ResponseJsonOptions, cancellationToken);
            return categories ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<Result<BriefingDto>> CreateBriefingAsync(CreateBriefingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsJsonAsync("briefings", request, ResponseJsonOptions, cancellationToken);
            return await ReadBriefingOrFailureAsync(response, SaveBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(SaveBriefingFailureMessage);
        }
    }

    public async Task<Result<BriefingDto>> UpdateBriefingAsync(Guid id, UpdateBriefingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PutAsJsonAsync($"briefings/{id}", request, ResponseJsonOptions, cancellationToken);
            return await ReadBriefingOrFailureAsync(response, SaveBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(SaveBriefingFailureMessage);
        }
    }

    public async Task<Result<BriefingDto>> SubmitBriefingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsync($"briefings/{id}/submit", cancellationToken);
            return await ReadBriefingOrFailureAsync(response, SubmitBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(SubmitBriefingFailureMessage);
        }
    }

    public async Task<Result<BriefingDto>> CancelBriefingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsync($"briefings/{id}/cancel", cancellationToken);
            return await ReadBriefingOrFailureAsync(response, CancelBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(CancelBriefingFailureMessage);
        }
    }

    public async Task<Result<BriefingTrackingDto>> GetBriefingTrackingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync($"briefings/{id}/tracking", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<BriefingTrackingDto>(response, LoadTrackingFailureMessage, cancellationToken: cancellationToken);

            var tracking = await response.Content.ReadFromJsonAsync<BriefingTrackingDto>(ResponseJsonOptions, cancellationToken);

            return tracking is null
                ? Result.Failure<BriefingTrackingDto>(LoadTrackingFailureMessage)
                : Result.Success(tracking);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingTrackingDto>(LoadTrackingFailureMessage);
        }
    }

    public async Task<Result<PagedResponse<PendingApprovalBriefingDto>>> GetPendingApprovalBriefingsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = QueryHelpers.AddQueryString("briefings/pending-approval", new Dictionary<string, string?>
            {
                ["page"] = page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
            });

            using var response = await _api.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<PagedResponse<PendingApprovalBriefingDto>>(response, LoadPendingBriefingsFailureMessage, cancellationToken: cancellationToken);

            var pendingPage = await response.Content.ReadFromJsonAsync<PagedResponse<PendingApprovalBriefingDto>>(ResponseJsonOptions, cancellationToken);

            return pendingPage is null
                ? Result.Failure<PagedResponse<PendingApprovalBriefingDto>>(LoadPendingBriefingsFailureMessage)
                : Result.Success(pendingPage);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<PendingApprovalBriefingDto>>(LoadPendingBriefingsFailureMessage);
        }
    }

    public async Task<Result<PagedResponse<OverdueBriefingDto>>> GetOverdueBriefingsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = QueryHelpers.AddQueryString("briefings/overdue", new Dictionary<string, string?>
            {
                ["page"] = page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
            });

            using var response = await _api.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<PagedResponse<OverdueBriefingDto>>(response, LoadOverdueBriefingsFailureMessage, cancellationToken: cancellationToken);

            var overduePage = await response.Content.ReadFromJsonAsync<PagedResponse<OverdueBriefingDto>>(ResponseJsonOptions, cancellationToken);

            return overduePage is null
                ? Result.Failure<PagedResponse<OverdueBriefingDto>>(LoadOverdueBriefingsFailureMessage)
                : Result.Success(overduePage);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<OverdueBriefingDto>>(LoadOverdueBriefingsFailureMessage);
        }
    }

    public async Task<Result<BriefingDto>> ApproveBriefingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsync($"briefings/{id}/approve", cancellationToken);
            return await ReadBriefingOrFailureAsync(response, ApproveBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(ApproveBriefingFailureMessage);
        }
    }

    public async Task<Result<BriefingDto>> RejectBriefingAsync(Guid id, string justification, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsJsonAsync($"briefings/{id}/reject", new RejectBriefingRequest(justification), ResponseJsonOptions, cancellationToken);
            return await ReadBriefingOrFailureAsync(response, RejectBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(RejectBriefingFailureMessage);
        }
    }

    public async Task<Result<BriefingDto>> RequestChangesBriefingAsync(Guid id, string justification, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsJsonAsync($"briefings/{id}/request-changes", new RequestChangesBriefingRequest(justification), ResponseJsonOptions, cancellationToken);
            return await ReadBriefingOrFailureAsync(response, RequestChangesBriefingFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingDto>(RequestChangesBriefingFailureMessage);
        }
    }

    private static string BuildBriefingsRequestUri(BriefingListQuery query)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
        };

        if (query.Status is { } status)
            parameters["status"] = status.ToString();

        if (query.CategoryId is { } categoryId)
            parameters["categoryId"] = categoryId.ToString();

        return QueryHelpers.AddQueryString("briefings", parameters);
    }

    private static async Task<Result<BriefingDto>> ReadBriefingOrFailureAsync(HttpResponseMessage response, string fallbackMessage, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            return await ApiProblemMapper.MapFailureAsync<BriefingDto>(response, fallbackMessage, cancellationToken: cancellationToken);

        var briefing = await response.Content.ReadFromJsonAsync<BriefingDto>(ResponseJsonOptions, cancellationToken);
        return briefing is null
            ? Result.Failure<BriefingDto>(fallbackMessage)
            : Result.Success(briefing);
    }
}
