using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Web.Tests.Auth;

public class AuthSessionStoreTests
{
    private const string SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!";

    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static string CreateToken(JwtSettings settings, TimeSpan expiresIn)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.Add(expiresIn),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthSessionStore CreateStore(FakeAuthTokenStorage storage, JwtSettings? settings = null) =>
        new(storage, new JwtClaimsPrincipalFactory(Options.Create(settings ?? CreateSettings())));

    [Fact]
    public async Task SignInAsync_WhenCalled_SetsTokensPersistsAndRaisesTokenChanged()
    {
        // Arrange
        var settings = CreateSettings();
        var storage = new FakeAuthTokenStorage();
        var store = CreateStore(storage, settings);
        var tokens = new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(15)), "refresh-token-value");
        var raisedCount = 0;
        store.TokenChanged += () => raisedCount++;

        // Act
        await store.SignInAsync(tokens);

        // Assert
        store.AccessToken.Should().Be(tokens.AccessToken);
        store.RefreshToken.Should().Be(tokens.RefreshToken);
        (await storage.GetAsync()).Should().Be(tokens);
        raisedCount.Should().Be(1);
    }

    [Fact]
    public async Task SignOutAsync_WhenCalled_ClearsTokensRemovesFromStorageAndRaisesTokenChanged()
    {
        // Arrange
        var settings = CreateSettings();
        var storage = new FakeAuthTokenStorage();
        var store = CreateStore(storage, settings);
        await store.SignInAsync(new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(15)), "refresh-token-value"));
        var raisedCount = 0;
        store.TokenChanged += () => raisedCount++;

        // Act
        await store.SignOutAsync();

        // Assert
        store.AccessToken.Should().BeNull();
        store.RefreshToken.Should().BeNull();
        (await storage.GetAsync()).Should().BeNull();
        raisedCount.Should().Be(1);
    }

    [Fact]
    public async Task RestoreAsync_WhenStorageHasValidToken_RestoresTokensAndRaisesTokenChanged()
    {
        // Arrange
        var settings = CreateSettings();
        var storage = new FakeAuthTokenStorage();
        var tokens = new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(15)), "refresh-token-value");
        await storage.SetAsync(tokens);
        var store = CreateStore(storage, settings);
        var raisedCount = 0;
        store.TokenChanged += () => raisedCount++;

        // Act
        await store.RestoreAsync();

        // Assert
        store.AccessToken.Should().Be(tokens.AccessToken);
        store.RefreshToken.Should().Be(tokens.RefreshToken);
        raisedCount.Should().Be(1);
    }

    [Fact]
    public async Task RestoreAsync_WhenStorageHasExpiredAccessToken_DoesNotRestoreAccessTokenOrRaiseTokenChanged()
    {
        // Arrange
        var settings = CreateSettings();
        var storage = new FakeAuthTokenStorage();
        await storage.SetAsync(new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(-5)), "refresh-token-value"));
        var store = CreateStore(storage, settings);
        var raisedCount = 0;
        store.TokenChanged += () => raisedCount++;

        // Act
        await store.RestoreAsync();

        // Assert
        store.AccessToken.Should().BeNull();
        raisedCount.Should().Be(0);
    }

    [Fact]
    public async Task RestoreAsync_WhenStorageHasExpiredAccessToken_StillRestoresRefreshTokenForAutomaticRefresh()
    {
        // Arrange
        var settings = CreateSettings();
        var storage = new FakeAuthTokenStorage();
        var tokens = new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(-5)), "refresh-token-value");
        await storage.SetAsync(tokens);
        var store = CreateStore(storage, settings);

        // Act
        await store.RestoreAsync();

        // Assert
        store.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task RestoreAsync_WhenStorageIsEmpty_DoesNothing()
    {
        // Arrange
        var store = CreateStore(new FakeAuthTokenStorage());

        // Act
        var action = () => store.RestoreAsync();

        // Assert
        await action.Should().NotThrowAsync();
        store.AccessToken.Should().BeNull();
    }

    [Fact]
    public async Task RestoreAsync_WhenStorageThrows_LeavesAccessTokenNullWithoutThrowing()
    {
        // Arrange
        var storage = new FakeAuthTokenStorage { ThrowOnGet = new InvalidOperationException("JS interop unavailable") };
        var store = CreateStore(storage);

        // Act
        var action = () => store.RestoreAsync();

        // Assert
        await action.Should().NotThrowAsync();
        store.AccessToken.Should().BeNull();
    }

    [Fact]
    public void RestorationCompleted_BeforeRestoreAsyncIsCalled_IsNotCompleted()
    {
        // Arrange
        var store = CreateStore(new FakeAuthTokenStorage());

        // Act
        var task = store.RestorationCompleted;

        // Assert
        task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task RestorationCompleted_AfterRestoreAsyncFindsValidToken_IsCompleted()
    {
        // Arrange
        var settings = CreateSettings();
        var storage = new FakeAuthTokenStorage();
        await storage.SetAsync(new AuthTokens(CreateToken(settings, TimeSpan.FromMinutes(15)), "refresh-token-value"));
        var store = CreateStore(storage, settings);

        // Act
        await store.RestoreAsync();

        // Assert
        store.RestorationCompleted.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RestorationCompleted_AfterRestoreAsyncFailsToReadStorage_IsStillCompleted()
    {
        // Arrange
        var storage = new FakeAuthTokenStorage { ThrowOnGet = new InvalidOperationException("JS interop unavailable") };
        var store = CreateStore(storage);

        // Act
        await store.RestoreAsync();

        // Assert
        store.RestorationCompleted.IsCompletedSuccessfully.Should().BeTrue();
    }
}
