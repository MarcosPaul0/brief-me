using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Web.Tests.Auth;

public class JwtAuthenticationStateProviderTests
{
    private const string SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!";

    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static string CreateToken(JwtSettings settings, TimeSpan expiresIn) =>
        CreateToken(settings, expiresIn, [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())]);

    private static string CreateToken(JwtSettings settings, TimeSpan expiresIn, IEnumerable<Claim> claims)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiresIn),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (AuthSessionStore Store, JwtAuthenticationStateProvider Provider) CreateSubjects(JwtSettings settings)
    {
        var factory = new JwtClaimsPrincipalFactory(Options.Create(settings));
        var store = new AuthSessionStore(new FakeAuthTokenStorage(), factory);
        var provider = new JwtAuthenticationStateProvider(store, factory);
        return (store, provider);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenNoTokenIsSet_ReturnsAnonymousState()
    {
        // Arrange
        var (_, provider) = CreateSubjects(CreateSettings());

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_AfterSignIn_ReturnsAuthenticatedStateWithExpectedClaims()
    {
        // Arrange
        var settings = CreateSettings();
        var (store, provider) = CreateSubjects(settings);
        var userId = Guid.NewGuid().ToString();
        var token = CreateToken(settings, TimeSpan.FromMinutes(15), [new Claim(JwtRegisteredClaimNames.Sub, userId), new Claim(ClaimTypes.Role, "Designer")]);
        await store.SignInAsync(new AuthTokens(token, "refresh-token-value"));

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId);
        state.User.IsInRole("Designer").Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenSignedInTokenHasSinceExpired_ReturnsAnonymousState()
    {
        // Arrange: expiry set 2 minutes in the past, safely beyond the 1-minute clock skew,
        // so the token is unambiguously expired on the next validation without a real-time wait.
        var settings = CreateSettings();
        var (store, provider) = CreateSubjects(settings);
        await store.SignInAsync(new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(-2)), "refresh-token-value"));

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task SignInAsync_WhenTokenChanges_RaisesAuthenticationStateChangedWithAuthenticatedState()
    {
        // Arrange
        var settings = CreateSettings();
        var (store, provider) = CreateSubjects(settings);
        AuthenticationState? notifiedState = null;
        provider.AuthenticationStateChanged += task => notifiedState = task.Result;

        // Act
        await store.SignInAsync(new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(15)), "refresh-token-value"));

        // Assert
        notifiedState.Should().NotBeNull();
        notifiedState!.User.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task SignOutAsync_WhenCalled_RaisesAuthenticationStateChangedWithAnonymousState()
    {
        // Arrange
        var settings = CreateSettings();
        var (store, provider) = CreateSubjects(settings);
        await store.SignInAsync(new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(15)), "refresh-token-value"));
        AuthenticationState? notifiedState = null;
        provider.AuthenticationStateChanged += task => notifiedState = task.Result;

        // Act
        await store.SignOutAsync();

        // Assert
        notifiedState.Should().NotBeNull();
        notifiedState!.User.Identity!.IsAuthenticated.Should().BeFalse();
    }
}
