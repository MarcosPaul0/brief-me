using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Auth;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;

namespace BriefMe.Web.Tests.Auth;

public class AuthServiceTests
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class RoutedStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Captured eagerly: the caller's `using` block disposes the request (and its content)
            // as soon as SendAsync returns, so reading the body afterwards would throw ObjectDisposedException.
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            await Task.Yield();
            return await respond(request);
        }
    }

    private static AuthSessionStore CreateSessionStore(FakeAuthTokenStorage? storage = null)
    {
        var settings = new JwtSettings
        {
            Issuer = "BriefMe.Web.Tests",
            Audience = "BriefMe.Web.Tests.Audience",
            SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!",
            AccessTokenMinutes = 15
        };

        return new AuthSessionStore(storage ?? new FakeAuthTokenStorage(), new JwtClaimsPrincipalFactory(Options.Create(settings)));
    }

    private static (AuthService Service, AuthSessionStore SessionStore, RoutedStubHandler Handler, TestNavigationManager Navigation) CreateSut(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var sessionStore = CreateSessionStore();
        var navigation = new TestNavigationManager();
        var service = new AuthService(httpClient, sessionStore, navigation);
        return (service, sessionStore, handler, navigation);
    }

    private static HttpResponseMessage AuthResultResponse(string accessToken = "new-access-token", string refreshToken = "new-refresh-token") =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new AuthenticationDto(accessToken, refreshToken, 900, Guid.NewGuid(), "Ana Designer", "ana@briefme.test", ["Designer"]),
                options: WireJsonOptions)
        };

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreAccepted_SignsInSessionAndReturnsSuccess()
    {
        // Arrange
        var (service, sessionStore, _, _) = CreateSut(_ => Task.FromResult(AuthResultResponse("access-1", "refresh-1")));

        // Act
        var result = await service.LoginAsync("ana@briefme.test", "correct-password");

        // Assert
        result.IsSuccess.Should().BeTrue();
        sessionStore.AccessToken.Should().Be("access-1");
        sessionStore.RefreshToken.Should().Be("refresh-1");
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreRejected_ReturnsFailureAndLeavesSessionUntouched()
    {
        // Arrange
        var (service, sessionStore, _, _) = CreateSut(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        // Act
        var result = await service.LoginAsync("ana@briefme.test", "wrong-password");

        // Assert
        result.IsFailure.Should().BeTrue();
        sessionStore.AccessToken.Should().BeNull();
        sessionStore.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_WhenSessionIsActive_SendsAuthorizedRequestWithRefreshTokenAndClearsSession()
    {
        // Arrange
        var (service, sessionStore, handler, _) = CreateSut(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));

        // Act
        await service.LogoutAsync();

        // Assert
        var (request, body) = handler.Requests.Should().ContainSingle().Subject;
        request.Headers.Authorization!.Parameter.Should().Be("access-1");
        body.Should().Contain("refresh-1");
        sessionStore.AccessToken.Should().BeNull();
        sessionStore.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_WhenAllDevicesIsTrue_SendsAllDevicesInRequestBody()
    {
        // Arrange
        var (service, sessionStore, handler, _) = CreateSut(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));

        // Act
        await service.LogoutAsync(allDevices: true);

        // Assert
        handler.Requests.Single().Body.Should().Contain("\"allDevices\":true");
    }

    [Fact]
    public async Task LogoutAsync_WhenApiCallFails_StillClearsSession()
    {
        // Arrange
        var (service, sessionStore, _, _) = CreateSut(_ => throw new HttpRequestException("network down"));
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));

        // Act
        await service.LogoutAsync();

        // Assert
        sessionStore.AccessToken.Should().BeNull();
        sessionStore.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_WhenNoActiveSession_DoesNotCallApi()
    {
        // Arrange
        var (service, _, handler, _) = CreateSut(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        // Act
        await service.LogoutAsync();

        // Assert
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task TryRefreshAsync_WhenNoRefreshTokenInSession_ClearsSessionNavigatesToLoginAndReturnsFalseWithoutHttpCall()
    {
        // Arrange
        var (service, _, handler, navigation) = CreateSut(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Act
        var succeeded = await service.TryRefreshAsync();

        // Assert
        succeeded.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
        navigation.LastNavigationUri.Should().Be("/login");
    }

    [Fact]
    public async Task TryRefreshAsync_WhenRefreshSucceeds_UpdatesSessionAndReturnsTrue()
    {
        // Arrange
        var (service, sessionStore, _, _) = CreateSut(_ => Task.FromResult(AuthResultResponse("access-2", "refresh-2")));
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));

        // Act
        var succeeded = await service.TryRefreshAsync();

        // Assert
        succeeded.Should().BeTrue();
        sessionStore.AccessToken.Should().Be("access-2");
        sessionStore.RefreshToken.Should().Be("refresh-2");
    }

    [Fact]
    public async Task TryRefreshAsync_WhenApiRejectsRefresh_ClearsSessionNavigatesToLoginAndReturnsFalse()
    {
        // Arrange
        var (service, sessionStore, _, navigation) = CreateSut(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));

        // Act
        var succeeded = await service.TryRefreshAsync();

        // Assert
        succeeded.Should().BeFalse();
        sessionStore.AccessToken.Should().BeNull();
        sessionStore.RefreshToken.Should().BeNull();
        navigation.LastNavigationUri.Should().Be("/login");
    }

    [Fact]
    public async Task TryRefreshAsync_WhenCalledConcurrently_IssuesOnlyOneHttpCallAndBothCallersObserveTheSameResult()
    {
        // Arrange: the handler blocks until both calls are in flight, proving single-flight
        // deterministically rather than relying on incidental async-scheduling timing.
        var handlerEntered = new TaskCompletionSource();
        var releaseHandler = new TaskCompletionSource();
        var (service, sessionStore, handler, _) = CreateSut(async _ =>
        {
            handlerEntered.TrySetResult();
            await releaseHandler.Task;
            return AuthResultResponse("access-2", "refresh-2");
        });
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));

        // Act
        var firstCall = service.TryRefreshAsync();
        await handlerEntered.Task;
        var secondCall = service.TryRefreshAsync();
        releaseHandler.SetResult();
        var results = await Task.WhenAll(firstCall, secondCall);

        // Assert
        handler.Requests.Should().ContainSingle();
        results.Should().AllSatisfy(succeeded => succeeded.Should().BeTrue());
    }

    [Fact]
    public async Task TryRefreshAsync_WhenCalledAfterAPriorRefreshCompleted_StartsANewHttpCall()
    {
        // Arrange
        var (service, sessionStore, handler, _) = CreateSut(_ => Task.FromResult(AuthResultResponse("access-2", "refresh-2")));
        await sessionStore.SignInAsync(new AuthTokens("access-1", "refresh-1"));
        await service.TryRefreshAsync();

        // Act
        await service.TryRefreshAsync();

        // Assert
        handler.Requests.Should().HaveCount(2);
    }
}
