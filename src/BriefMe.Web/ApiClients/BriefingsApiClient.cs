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
    private const string SaveBriefingFailureMessage = "Não foi possível salvar o briefing. Tente novamente.";
    private const string SubmitBriefingFailureMessage = "Não foi possível enviar o briefing para aprovação. Tente novamente.";

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
