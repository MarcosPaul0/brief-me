using BriefMe.Web.Auth;
using BriefMe.Web.Http;
using Microsoft.Extensions.Options;

namespace BriefMe.Web.Tests.TestDoubles;

/// <summary>
/// Shared arrange helpers for the auth-aware Blazor API layer: a circuit-scoped
/// <see cref="AuthSessionStore"/> and the <see cref="AuthenticatedApiClient"/> the typed clients send through.
/// </summary>
public static class AuthTestHelpers
{
    public static AuthSessionStore CreateSessionStore(FakeAuthTokenStorage? storage = null)
    {
        var settings = new BriefMe.Infrastructure.Auth.JwtSettings
        {
            Issuer = "BriefMe.Web.Tests",
            Audience = "BriefMe.Web.Tests.Audience",
            SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!",
            AccessTokenMinutes = 15
        };

        return new AuthSessionStore(storage ?? new FakeAuthTokenStorage(), new JwtClaimsPrincipalFactory(Options.Create(settings)));
    }

    /// <summary>
    /// Wraps <paramref name="httpClient"/> in an <see cref="AuthenticatedApiClient"/> for the typed-client
    /// tests, which only exercise routing/mapping — auth is a no-op stub here.
    /// </summary>
    public static AuthenticatedApiClient CreateApiClient(HttpClient httpClient) =>
        new(new StubHttpClientFactory(httpClient), CreateSessionStore(), new FakeAuthService());
}

public sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
