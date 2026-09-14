using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Auth;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Auth;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BriefMe.Web.Auth;

public sealed class AuthService : IAuthService
{
    private const string GenericLoginFailureMessage = "E-mail ou senha inválidos.";

    // ASP.NET Core serializes controller responses with camelCase property names by default;
    // this matches them back onto AuthenticationResult's PascalCase record parameters.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AuthSessionStore _sessionStore;
    private readonly NavigationManager _navigationManager;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private Task<bool>? _inFlightRefresh;

    public AuthService(HttpClient httpClient, AuthSessionStore sessionStore, NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _sessionStore = sessionStore;
        _navigationManager = navigationManager;
    }

    public async Task<Result> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("auth/login", new LoginRequest(email, password), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Result.Unauthorized(GenericLoginFailureMessage);

        var result = await response.Content.ReadFromJsonAsync<AuthenticationDto>(ResponseJsonOptions, cancellationToken);
        if (result is null)
            return Result.Unauthorized(GenericLoginFailureMessage);

        await _sessionStore.SignInAsync(new AuthTokens(result.AccessToken, result.RefreshToken));
        return Result.Success();
    }

    public async Task LogoutAsync(bool allDevices = false, CancellationToken cancellationToken = default)
    {
        var accessToken = _sessionStore.AccessToken;
        var refreshToken = _sessionStore.RefreshToken;

        if (accessToken is not null && refreshToken is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout")
                {
                    Content = JsonContent.Create(new LogoutRequest(refreshToken, allDevices))
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Best-effort server-side revoke: the local session is cleared below regardless of the outcome.
            }
        }

        await _sessionStore.SignOutAsync();
    }

    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default) =>
        GetOrStartRefreshAsync(cancellationToken);

    private async Task<bool> GetOrStartRefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        Task<bool> refreshTask;
        try
        {
            refreshTask = _inFlightRefresh ??= RefreshCoreAsync(cancellationToken);
        }
        finally
        {
            _refreshGate.Release();
        }

        var succeeded = await refreshTask;

        await _refreshGate.WaitAsync(CancellationToken.None);
        try
        {
            if (_inFlightRefresh == refreshTask)
                _inFlightRefresh = null;
        }
        finally
        {
            _refreshGate.Release();
        }

        return succeeded;
    }

    private async Task<bool> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var refreshToken = _sessionStore.RefreshToken;
        if (refreshToken is null)
        {
            await SignOutAndRedirectToLoginAsync();
            return false;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("auth/refresh", new RefreshTokenRequest(refreshToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await SignOutAndRedirectToLoginAsync();
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthenticationDto>(ResponseJsonOptions, cancellationToken);
            if (result is null)
            {
                await SignOutAndRedirectToLoginAsync();
                return false;
            }

            await _sessionStore.SignInAsync(new AuthTokens(result.AccessToken, result.RefreshToken));
            return true;
        }
        catch (HttpRequestException)
        {
            await SignOutAndRedirectToLoginAsync();
            return false;
        }
    }

    private async Task SignOutAndRedirectToLoginAsync()
    {
        await _sessionStore.SignOutAsync();
        _navigationManager.NavigateTo("/login", forceLoad: true);
    }
}
