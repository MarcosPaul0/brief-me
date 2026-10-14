using System.Globalization;
using System.Net.Http.Json;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Contracts.Common;
using BriefMe.Web.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Sends through <see cref="AuthenticatedApiClient"/> (bearer + refresh-on-401) and maps non-success
/// responses via <see cref="ApiProblemMapper"/>, so every screen surfaces backend failures the same way.
/// </summary>
public sealed class BriefingsApiClient(AuthenticatedApiClient api) : IBriefingsApiClient
{
    private const string LoadBriefingFailureMessage = "Não foi possível carregar o briefing. Tente novamente.";
    private const string LoadBriefingsFailureMessage = "Não foi possível carregar os briefings. Tente novamente.";
    private const string SaveBriefingFailureMessage = "Não foi possível salvar o briefing. Tente novamente.";
    private const string SubmitBriefingFailureMessage = "Não foi possível enviar o briefing. Tente novamente.";
    private const string CancelBriefingFailureMessage = "Não foi possível cancelar o briefing. Tente novamente.";
    private const string AcceptFailureMessage = "Não foi possível aceitar a entrega. Tente novamente.";
    private const string RequestChangesFailureMessage = "Não foi possível solicitar ajustes. Tente novamente.";
    private const string ClaimFailureMessage = "Não foi possível assumir o briefing. Tente novamente.";
    private const string DeliverFailureMessage = "Não foi possível entregar o briefing. Tente novamente.";

    public Task<Result<BriefingDetailDto>> GetBriefingAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetResultAsync<BriefingDetailDto, BriefingDetailDto>($"briefings/{id}", LoadBriefingFailureMessage, cancellationToken);

    public Task<Result<PagedResponse<BriefingListItemDto>>> GetBriefingsAsync(BriefingListQuery query, CancellationToken cancellationToken = default)
    {
        var parameters = Paging(query.Page, query.PageSize);

        if (query.Status is { } status)
            parameters["status"] = status.ToString();

        if (query.CategoryId is { } categoryId)
            parameters["categoryId"] = categoryId.ToString();

        return GetPageAsync(QueryHelpers.AddQueryString("briefings", parameters), cancellationToken);
    }

    public Task<Result<PagedResponse<BriefingListItemDto>>> GetQueueAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetPageAsync(QueryHelpers.AddQueryString("briefings/queue", Paging(page, pageSize)), cancellationToken);

    public Task<Result<PagedResponse<BriefingListItemDto>>> GetAssignedAsync(
        int page,
        int pageSize,
        BriefingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = Paging(page, pageSize);

        if (status is { } value)
            parameters["status"] = value.ToString();

        return GetPageAsync(QueryHelpers.AddQueryString("briefings/assigned", parameters), cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await api.GetAsync("categories/active", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return [];

            var categories = await response.Content.ReadFromJsonAsync<List<CategoryListItemDto>>(ApiProblemMapper.JsonOptions, cancellationToken);
            return categories ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public Task<Result<BriefingDto>> CreateBriefingAsync(CreateBriefingRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(client => client.PostAsJsonAsync("briefings", request, ApiProblemMapper.JsonOptions, cancellationToken), SaveBriefingFailureMessage, cancellationToken);

    public Task<Result<BriefingDto>> UpdateBriefingAsync(Guid id, UpdateBriefingRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(client => client.PutAsJsonAsync($"briefings/{id}", request, ApiProblemMapper.JsonOptions, cancellationToken), SaveBriefingFailureMessage, cancellationToken);

    public Task<Result<BriefingDto>> SubmitBriefingAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(client => client.PostAsync($"briefings/{id}/submit", cancellationToken), SubmitBriefingFailureMessage, cancellationToken);

    public Task<Result<BriefingDto>> CancelBriefingAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(client => client.PostAsync($"briefings/{id}/cancel", cancellationToken), CancelBriefingFailureMessage, cancellationToken);

    public Task<Result<BriefingDto>> AcceptDeliveryAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(client => client.PostAsync($"briefings/{id}/accept", cancellationToken), AcceptFailureMessage, cancellationToken);

    public Task<Result<BriefingDto>> RequestChangesAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync(
            client => client.PostAsJsonAsync($"briefings/{id}/request-changes", new RequestBriefingChangesRequest(reason), ApiProblemMapper.JsonOptions, cancellationToken),
            RequestChangesFailureMessage,
            cancellationToken);

    public Task<Result<BriefingDto>> ClaimBriefingAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(client => client.PostAsync($"briefings/{id}/claim", cancellationToken), ClaimFailureMessage, cancellationToken);

    public Task<Result<BriefingDto>> DeliverBriefingAsync(Guid id, string deliveryNote, CancellationToken cancellationToken = default) =>
        SendAsync(
            client => client.PostAsJsonAsync($"briefings/{id}/deliver", new DeliverBriefingRequest(deliveryNote), ApiProblemMapper.JsonOptions, cancellationToken),
            DeliverFailureMessage,
            cancellationToken);

    private Task<Result<PagedResponse<BriefingListItemDto>>> GetPageAsync(string requestUri, CancellationToken cancellationToken) =>
        api.GetResultAsync<PagedResponse<BriefingListItemDto>, PagedResponse<BriefingListItemDto>>(requestUri, LoadBriefingsFailureMessage, cancellationToken);

    private Task<Result<BriefingDto>> SendAsync(
        Func<AuthenticatedApiClient, Task<HttpResponseMessage>> send,
        string fallbackMessage,
        CancellationToken cancellationToken) =>
        api.SendForResultAsync<BriefingDto>(send, fallbackMessage, cancellationToken);

    private static Dictionary<string, string?> Paging(int page, int pageSize) => new()
    {
        ["page"] = page.ToString(CultureInfo.InvariantCulture),
        ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
    };
}
