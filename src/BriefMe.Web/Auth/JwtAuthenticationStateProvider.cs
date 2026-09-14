using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BriefMe.Web.Auth;

public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly AuthSessionStore _sessionStore;
    private readonly JwtClaimsPrincipalFactory _claimsPrincipalFactory;

    public JwtAuthenticationStateProvider(AuthSessionStore sessionStore, JwtClaimsPrincipalFactory claimsPrincipalFactory)
    {
        _sessionStore = sessionStore;
        _claimsPrincipalFactory = claimsPrincipalFactory;
        _sessionStore.TokenChanged += OnTokenChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_sessionStore.AccessToken is { } token && _claimsPrincipalFactory.TryCreatePrincipal(token, out var principal))
            return Task.FromResult(new AuthenticationState(principal));

        return Task.FromResult(Anonymous);
    }

    private void OnTokenChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
