using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Application.UseCases.Categories;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.ApiClients;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.Briefings;

public class BriefingsApiClientTests
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

    private static (BriefingsApiClient Client, RoutedStubHandler Handler) CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var client = new BriefingsApiClient(AuthTestHelpers.CreateApiClient(httpClient));
        return (client, handler);
    }

    private static HttpResponseMessage BriefingResponse(Guid id, BriefingStatus status = BriefingStatus.Draft) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id,
                    title = "Campanha",
                    description = (string?)null,
                    objective = (string?)null,
                    requestingArea = (string?)null,
                    desiredDeliveryDate = (DateOnly?)null,
                    priority = (int)Priority.Medium,
                    categoryId = Guid.NewGuid(),
                    requesterId = Guid.NewGuid(),
                    status = (int)status
                },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage PagedBriefingsResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 },
                options: WireJsonOptions)
        };

    [Fact]
    public async Task GetBriefingAsync_WhenFound_ReturnsSuccess()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => BriefingResponse(id));

        // Act
        var result = await client.GetBriefingAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}");
    }

    [Fact]
    public async Task GetBriefingAsync_WhenForbidden_MapsToForbiddenFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.GetBriefingAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenFailure_ReturnsEmptyList()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var categories = await client.GetCategoriesAsync();

        // Assert
        categories.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenOk_ReturnsCategories()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new[] { new { id = Guid.NewGuid(), name = "Campanha" } },
                options: WireJsonOptions)
        });

        // Act
        var categories = await client.GetCategoriesAsync();

        // Assert
        categories.Should().ContainSingle().Which.Name.Should().Be("Campanha");
    }

    [Fact]
    public async Task CreateBriefingAsync_PostsToBriefingsAndReturnsDto()
    {
        // Arrange
        var createdId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = BriefingResponse(createdId).Content
        });

        // Act
        var result = await client.CreateBriefingAsync(new CreateBriefingRequest("Campanha", Priority.Medium, Guid.NewGuid()));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(createdId);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/briefings");
    }

    [Fact]
    public async Task SubmitBriefingAsync_PostsToSubmitEndpoint()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = BriefingResponse(id, BriefingStatus.Submitted).Content
        });

        // Act
        var result = await client.SubmitBriefingAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Submitted);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/submit");
    }

    [Fact]
    public async Task GetBriefingsAsync_WhenOk_ReturnsPagedBriefings()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new
                        {
                            id,
                            title = "Campanha",
                            description = (string?)null,
                            objective = (string?)null,
                            requestingArea = (string?)null,
                            desiredDeliveryDate = (DateOnly?)null,
                            priority = (int)Priority.High,
                            categoryId = Guid.NewGuid(),
                            requesterId = Guid.NewGuid(),
                            status = (int)BriefingStatus.Draft
                        }
                    },
                    page = 1,
                    pageSize = 20,
                    totalCount = 1
                },
                options: WireJsonOptions)
        });

        // Act
        var result = await client.GetBriefingsAsync(new BriefingListQuery(1, 20));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.Id.Should().Be(id);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/briefings");
    }

    [Fact]
    public async Task GetBriefingsAsync_WhenNoFilters_SendsOnlyPagination()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedBriefingsResponse());

        // Act
        await client.GetBriefingsAsync(new BriefingListQuery(2, 50));

        // Assert
        handler.Requests[0].RequestUri!.Query.Should().Be("?page=2&pageSize=50");
    }

    [Fact]
    public async Task GetBriefingsAsync_WhenFiltered_SendsStatusAndCategory()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => PagedBriefingsResponse());

        // Act
        await client.GetBriefingsAsync(new BriefingListQuery(1, 20, BriefingStatus.Submitted, categoryId));

        // Assert
        var query = handler.Requests[0].RequestUri!.Query;
        query.Should().Contain($"status={nameof(BriefingStatus.Submitted)}");
        query.Should().Contain($"categoryId={categoryId}");
    }

    [Fact]
    public async Task GetBriefingsAsync_WhenRequestFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await client.GetBriefingsAsync(new BriefingListQuery(1, 20));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CancelBriefingAsync_PostsToCancelEndpoint()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = BriefingResponse(id, BriefingStatus.Cancelled).Content
        });

        // Act
        var result = await client.CancelBriefingAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Cancelled);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/cancel");
    }

    [Fact]
    public async Task CancelBriefingAsync_WhenStatusNoLongerAllowsIt_MapsToConflictFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

        // Act
        var result = await client.CancelBriefingAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateBriefingAsync_WhenConflict_MapsToConflictFailure()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

        // Act
        var result = await client.UpdateBriefingAsync(Guid.NewGuid(), new UpdateBriefingRequest("Campanha", Priority.High));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
        handler.Requests[0].Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task GetQueueAsync_Always_GetsTheQueueWithPaging()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedBriefingsResponse());

        // Act
        var result = await client.GetQueueAsync(3, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/briefings/queue?page=3&pageSize=10");
    }

    [Fact]
    public async Task GetAssignedAsync_WhenStatusGiven_SendsIt()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedBriefingsResponse());

        // Act
        await client.GetAssignedAsync(1, 20, BriefingStatus.Delivered);

        // Assert
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/briefings/assigned?page=1&pageSize=20&status=Delivered");
    }

    [Fact]
    public async Task ClaimBriefingAsync_WhenCalled_PostsToClaimRoute()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => BriefingResponse(id, BriefingStatus.InProgress));

        // Act
        var result = await client.ClaimBriefingAsync(id);

        // Assert
        result.Value.Status.Should().Be(BriefingStatus.InProgress);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/claim");
    }

    [Fact]
    public async Task AcceptDeliveryAsync_WhenCalled_PostsToAcceptRoute()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => BriefingResponse(id, BriefingStatus.Completed));

        // Act
        var result = await client.AcceptDeliveryAsync(id);

        // Assert
        result.Value.Status.Should().Be(BriefingStatus.Completed);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/accept");
    }

    [Fact]
    public async Task DeliverBriefingAsync_WhenCalled_PostsTheNote()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => BriefingResponse(id, BriefingStatus.Delivered));

        // Act
        await client.DeliverBriefingAsync(id, "https://files.example/v1");

        // Assert
        var body = await handler.Requests[0].Content!.ReadFromJsonAsync<DeliverBriefingRequest>(WireJsonOptions);
        body!.DeliveryNote.Should().Be("https://files.example/v1");
    }

    [Fact]
    public async Task RequestChangesAsync_WhenCalled_PostsTheReason()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => BriefingResponse(id, BriefingStatus.InProgress));

        // Act
        await client.RequestChangesAsync(id, "Use the new logo");

        // Assert
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/request-changes");
        var body = await handler.Requests[0].Content!.ReadFromJsonAsync<RequestBriefingChangesRequest>(WireJsonOptions);
        body!.Reason.Should().Be("Use the new logo");
    }

    [Fact]
    public async Task ClaimBriefingAsync_WhenAnotherDesignerWon_MapsToConflictFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { title = "Falha", status = 409, detail = "Este briefing já foi assumido." }, options: WireJsonOptions)
        });

        // Act
        var result = await client.ClaimBriefingAsync(Guid.NewGuid());

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
    }
}
