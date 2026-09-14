using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BriefMe.Web.Auth;
using BriefMe.Web.Http;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.Http;

public class AuthenticatedApiClientTests
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record CapturedRequest(HttpMethod Method, Uri? Uri, AuthenticationHeaderValue? Authorization, string? Body);

    private sealed class RecordingHandler(params HttpStatusCode[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, request.Headers.Authorization, body));

            var status = _responses.Count > 0 ? _responses.Dequeue() : HttpStatusCode.OK;
            return new HttpResponseMessage(status);
        }
    }

    private static AuthenticatedApiClient CreateSut(RecordingHandler handler, AuthSessionStore sessionStore, FakeAuthService authService)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        return new AuthenticatedApiClient(new StubHttpClientFactory(httpClient), sessionStore, authService);
    }

    [Fact]
    public async Task GetAsync_WhenSessionHasAccessToken_AttachesBearerAuthorizationHeader()
    {
        // Arrange
        var sessionStore = AuthTestHelpers.CreateSessionStore();
        await sessionStore.SignInAsync(new AuthTokens("access-token", "refresh-token"));
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var sut = CreateSut(handler, sessionStore, new FakeAuthService());

        // Act
        await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        handler.Requests.Single().Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "access-token"));
    }

    [Fact]
    public async Task GetAsync_WhenSessionHasNoAccessToken_SendsRequestWithoutAuthorizationHeader()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var sut = CreateSut(handler, AuthTestHelpers.CreateSessionStore(), new FakeAuthService());

        // Act
        await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        handler.Requests.Single().Authorization.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenResponseIsNotUnauthorized_PassesResponseThroughWithoutRefreshing()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var authService = new FakeAuthService();
        var sut = CreateSut(handler, AuthTestHelpers.CreateSessionStore(), authService);

        // Act
        using var response = await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        authService.TryRefreshCallCount.Should().Be(0);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAsync_WhenUnauthorizedAndRefreshSucceeds_RetriesOnceAndReturnsRetriedResponse()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var authService = new FakeAuthService { RefreshResult = true };
        var sut = CreateSut(handler, AuthTestHelpers.CreateSessionStore(), authService);

        // Act
        using var response = await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Requests.Should().HaveCount(2);
        authService.TryRefreshCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenUnauthorizedAndRefreshSucceeds_RetryCarriesTheRefreshedToken()
    {
        // Arrange
        var sessionStore = AuthTestHelpers.CreateSessionStore();
        await sessionStore.SignInAsync(new AuthTokens("stale-token", "refresh-token"));
        var authService = new FakeAuthService
        {
            RefreshResult = true,
            OnRefreshAsync = () => sessionStore.SignInAsync(new AuthTokens("fresh-token", "new-refresh-token"))
        };
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var sut = CreateSut(handler, sessionStore, authService);

        // Act
        await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        handler.Requests[0].Authorization!.Parameter.Should().Be("stale-token");
        handler.Requests[1].Authorization!.Parameter.Should().Be("fresh-token");
    }

    [Fact]
    public async Task GetAsync_WhenUnauthorizedAndRefreshFails_ReturnsOriginalUnauthorizedWithoutRetry()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized);
        var authService = new FakeAuthService { RefreshResult = false };
        var sut = CreateSut(handler, AuthTestHelpers.CreateSessionStore(), authService);

        // Act
        using var response = await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAsync_WhenRetriedRequestAlsoUnauthorized_ReturnsThatResponseWithoutASecondRetry()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized);
        var authService = new FakeAuthService { RefreshResult = true };
        var sut = CreateSut(handler, AuthTestHelpers.CreateSessionStore(), authService);

        // Act
        using var response = await sut.GetAsync("/users", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostAsJsonAsync_WhenUnauthorizedAndRefreshSucceeds_RetriesWithTheSameMethodAndBody()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var authService = new FakeAuthService { RefreshResult = true };
        var sut = CreateSut(handler, AuthTestHelpers.CreateSessionStore(), authService);

        // Act
        await sut.PostAsJsonAsync("/users", new { name = "Ana" }, WireJsonOptions, CancellationToken.None);

        // Assert
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[1].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Body.Should().Be(handler.Requests[1].Body);
    }
}
