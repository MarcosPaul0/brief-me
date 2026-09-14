using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Roles;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Common;
using BriefMe.Web.Contracts.Users;
using BriefMe.Web.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace BriefMe.Web.ApiClients;

public sealed class UsersApiClient : IUsersApiClient
{
    public const string HttpClientName = "Api";

    private const string LoadUsersFailureMessage = "Não foi possível carregar os usuários. Tente novamente.";
    private const string LoadUserFailureMessage = "Não foi possível carregar o usuário. Tente novamente.";
    private const string SaveUserFailureMessage = "Não foi possível salvar o usuário. Tente novamente.";
    private const string SaveUserRolesFailureMessage = "Não foi possível salvar os perfis do usuário. Tente novamente.";

    // The field a duplicate-email 409 (which carries no per-field errors dict) is attributed to.
    private const string EmailField = nameof(CreateUserRequest.Email);

    // ASP.NET Core serializes controller responses in camelCase; this binds them back onto the
    // PascalCase record parameters of PagedResponse/UserListItemDto/RoleListItemDto.
    private static readonly JsonSerializerOptions ResponseJsonOptions = ApiProblemMapper.JsonOptions;

    private readonly AuthenticatedApiClient _api;

    public UsersApiClient(AuthenticatedApiClient api)
    {
        // AuthenticatedApiClient owns bearer + refresh over the "Api" client (Program.cs / USR-039),
        // so auth is transparent to callers here.
        _api = api;
    }

    public async Task<Result<PagedResponse<UserListItemDto>>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync(BuildUsersRequestUri(query), cancellationToken);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<PagedResponse<UserListItemDto>>(LoadUsersFailureMessage);

            var page = await response.Content.ReadFromJsonAsync<PagedResponse<UserListItemDto>>(ResponseJsonOptions, cancellationToken);

            return page is null
                ? Result.Failure<PagedResponse<UserListItemDto>>(LoadUsersFailureMessage)
                : Result.Success(page);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<UserListItemDto>>(LoadUsersFailureMessage);
        }
    }

    public async Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync("roles", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return [];

            var roles = await response.Content.ReadFromJsonAsync<List<RoleListItemDto>>(ResponseJsonOptions, cancellationToken);
            return roles ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<Result<UserDto>> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync($"users/{id}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await MapFailureAsync<UserDto>(response, LoadUserFailureMessage, cancellationToken);

            var user = await response.Content.ReadFromJsonAsync<UserDto>(ResponseJsonOptions, cancellationToken);
            return user is null
                ? Result.Failure<UserDto>(LoadUserFailureMessage)
                : Result.Success(user);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<UserDto>(LoadUserFailureMessage);
        }
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsJsonAsync("users", request, ResponseJsonOptions, cancellationToken);
            return await ReadUserOrFailureAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<UserDto>(SaveUserFailureMessage);
        }
    }

    public async Task<Result<UserDto>> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PutAsJsonAsync($"users/{id}", request, ResponseJsonOptions, cancellationToken);
            return await ReadUserOrFailureAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<UserDto>(SaveUserFailureMessage);
        }
    }

    public async Task<Result<UserDto>> UpdateUserRolesAsync(Guid id, UpdateUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PutAsJsonAsync($"users/{id}/roles", request, ResponseJsonOptions, cancellationToken);
            return await ReadUserOrFailureAsync(response, cancellationToken, SaveUserRolesFailureMessage);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<UserDto>(SaveUserRolesFailureMessage);
        }
    }

    private static async Task<Result<UserDto>> ReadUserOrFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken, string? failureMessage = null)
    {
        var fallback = failureMessage ?? SaveUserFailureMessage;

        if (!response.IsSuccessStatusCode)
            return await MapFailureAsync<UserDto>(response, fallback, cancellationToken);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(ResponseJsonOptions, cancellationToken);
        return user is null
            ? Result.Failure<UserDto>(fallback)
            : Result.Success(user);
    }

    /// <summary>
    /// Delegates to the shared <see cref="ApiProblemMapper"/>, attributing a 409 (duplicate e-mail — the
    /// only conflict these endpoints raise) to the E-mail field so the form renders it inline.
    /// </summary>
    private static Task<Result<T>> MapFailureAsync<T>(HttpResponseMessage response, string fallbackMessage, CancellationToken cancellationToken) =>
        ApiProblemMapper.MapFailureAsync<T>(response, fallbackMessage, EmailField, cancellationToken);

    private static string BuildUsersRequestUri(UserListQuery query)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
            ["sortField"] = query.SortField.ToString(),
            ["sortDescending"] = query.SortDescending ? "true" : "false"
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
            parameters["search"] = query.Search.Trim();

        if (query.Status is { } status)
            parameters["status"] = status.ToString();

        if (query.RoleId is { } roleId)
            parameters["roleId"] = roleId.ToString();

        return QueryHelpers.AddQueryString("users", parameters);
    }
}
