using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.ApiClients;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.Notifications;

/// <summary>
/// Covers the transport of the bell (BRF-054): what each call sends, how a success maps and — the part that
/// matters most for a badge refreshed by a timer — that no failure escapes as an exception.
/// </summary>
public class NotificationsApiClientTests
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class RoutedStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static (NotificationsApiClient Client, RoutedStubHandler Handler) CreateSut(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var client = new NotificationsApiClient(AuthTestHelpers.CreateApiClient(httpClient));
        return (client, handler);
    }

    private static HttpResponseMessage PagedNotificationsResponse(Guid notificationId, DateTime? readAt) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new
                        {
                            id = notificationId,
                            eventType = (int)NotificationEventType.BriefingRejected,
                            title = "Briefing rejeitado",
                            message = "Seu briefing \"Campanha de Natal\" foi rejeitado. Justificativa: fora de escopo.",
                            createdAt = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc),
                            readAt
                        }
                    },
                    page = 1,
                    pageSize = 10,
                    totalCount = 4
                },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage UnreadCountResponse(int count) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new { count }, options: WireJsonOptions) };

    [Fact]
    public async Task GetNotificationsAsync_WhenOk_ReturnsThePage()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => PagedNotificationsResponse(notificationId, readAt: null));

        // Act
        var result = await client.GetNotificationsAsync(1, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(4);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(notificationId);
        item.EventType.Should().Be(NotificationEventType.BriefingRejected);
        item.Title.Should().Be("Briefing rejeitado");
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/notifications");
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenTheEntryIsUnread_MapsItAsUnread()
    {
        // Arrange
        var (client, _) = CreateSut(_ => PagedNotificationsResponse(Guid.NewGuid(), readAt: null));

        // Act
        var result = await client.GetNotificationsAsync(1, 10);

        // Assert
        // The panel marks what was new by this flag, so it has to survive the wire.
        result.Value.Items.Should().ContainSingle().Which.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenTheEntryWasRead_MapsItAsRead()
    {
        // Arrange
        var readAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc);
        var (client, _) = CreateSut(_ => PagedNotificationsResponse(Guid.NewGuid(), readAt));

        // Act
        var result = await client.GetNotificationsAsync(1, 10);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ReadAt.Should().Be(readAt);
        item.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task GetNotificationsAsync_SendsPagingQueryString()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedNotificationsResponse(Guid.NewGuid(), null));

        // Act
        await client.GetNotificationsAsync(2, 25);

        // Assert
        handler.Requests[0].RequestUri!.Query.Should().Contain("page=2").And.Contain("pageSize=25");
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenRequestFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await client.GetNotificationsAsync(1, 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Não foi possível carregar as notificações. Tente novamente.");
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenTransportFaults_ReturnsFailureInsteadOfThrowing()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("connection refused"));

        // Act
        var result = await client.GetNotificationsAsync(1, 10);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenOk_ReturnsTheCount()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => UnreadCountResponse(7));

        // Act
        var result = await client.GetUnreadCountAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(7);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/notifications/unread-count");
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenUnauthorized_MapsTheErrorType()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        // Act
        var result = await client.GetUnreadCountAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenTransportFaults_ReturnsFailureInsteadOfThrowing()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("connection refused"));

        // Act
        var result = await client.GetUnreadCountAsync();

        // Assert
        // The poll runs on a timer: an exception here would fault the circuit over a network blip.
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenNoContent_ReturnsSuccess()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        var result = await client.MarkAllAsReadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/notifications/read");
    }

    [Fact]
    public async Task MarkAllAsReadAsync_SendsNoBody()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        await client.MarkAllAsReadAsync();

        // Assert
        // The set to mark is "the caller's unread ones", resolved from the token — never sent by the client.
        handler.Requests[0].Content.Should().BeNull();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenRequestFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await client.MarkAllAsReadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Não foi possível marcar as notificações como lidas. Tente novamente.");
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenUnauthorized_MapsTheErrorType()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        // Act
        var result = await client.MarkAllAsReadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenTransportFaults_ReturnsFailureInsteadOfThrowing()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("connection refused"));

        // Act
        var result = await client.MarkAllAsReadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
