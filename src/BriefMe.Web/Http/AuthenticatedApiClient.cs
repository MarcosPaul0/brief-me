using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BriefMe.Web.Auth;
using BriefMe.Web.ApiClients;

namespace BriefMe.Web.Http;

/// <summary>
/// Circuit-scoped wrapper over the "Api" <see cref="HttpClient"/> that attaches the current bearer
/// token and, on a 401, refreshes once and retries. It replaces the <c>IHttpClientFactory</c>
/// delegating handlers (USR-039): those run in the factory's own handler scope — not the Blazor
/// circuit scope — so they saw an empty <see cref="AuthSessionStore"/> (no token attached, every
/// call anonymous) and an uninitialized <c>RemoteNavigationManager</c> (crash on sign-out redirect).
/// Every dependency here resolves from the circuit scope, so token state and the redirect are correct.
/// </summary>
public sealed class AuthenticatedApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthSessionStore _sessionStore;
    private readonly IAuthService _authService;

    public AuthenticatedApiClient(IHttpClientFactory httpClientFactory, AuthSessionStore sessionStore, IAuthService authService)
    {
        // The "Api" client no longer carries auth handlers; this type owns bearer + refresh instead.
        _httpClient = httpClientFactory.CreateClient(UsersApiClient.HttpClientName);
        _sessionStore = sessionStore;
        _authService = authService;
    }

    public Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Get, requestUri), cancellationToken);

    public Task<HttpResponseMessage> PostAsync(string requestUri, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, requestUri), cancellationToken);

    /// <summary>
    /// Posts a body the caller builds — <c>multipart/form-data</c> for a task delivery (BRF-034), where
    /// <see cref="PostAsJsonAsync"/> does not apply. Takes a <b>factory</b> for the same reason
    /// <see cref="SendAsync"/> does: an <see cref="HttpContent"/> can only be sent once, so the refresh
    /// retry needs a freshly built one. A caller wrapping a stream must therefore be able to produce it
    /// twice — buffering the bytes first is the simplest way.
    /// </summary>
    public Task<HttpResponseMessage> PostAsync(string requestUri, Func<HttpContent> contentFactory, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = contentFactory() }, cancellationToken);

    public Task<HttpResponseMessage> PostAsJsonAsync<TValue>(string requestUri, TValue value, JsonSerializerOptions options, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = JsonContent.Create(value, options: options) }, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, requestUri), cancellationToken);

    public Task<HttpResponseMessage> PutAsJsonAsync<TValue>(string requestUri, TValue value, JsonSerializerOptions options, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Put, requestUri) { Content = JsonContent.Create(value, options: options) }, cancellationToken);

    // The request is built from a factory so the retry gets a fresh, unsent message (an HttpRequestMessage
    // and its content can only be sent once) — no manual cloning of a consumed request.
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var request = requestFactory();
        AttachBearer(request);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // On a 401, refresh once. TryRefreshAsync stores a fresh access token on success, or clears the
        // session and redirects to /login on hard failure (now with the circuit's NavigationManager).
        if (!await _authService.TryRefreshAsync(cancellationToken))
            return response;

        response.Dispose();

        var retry = requestFactory();
        AttachBearer(retry);
        return await _httpClient.SendAsync(retry, cancellationToken);
    }

    private void AttachBearer(HttpRequestMessage request)
    {
        if (_sessionStore.AccessToken is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
