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

    private static HttpResponseMessage OverdueBriefingsResponse(Guid id, DateTime overdueSince) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new
                        {
                            id,
                            title = "Campanha de verão",
                            desiredDeliveryDate = (DateOnly?)null,
                            priority = (int)Priority.Urgent,
                            status = (int)BriefingStatus.InProgress,
                            categoryId = Guid.NewGuid(),
                            categoryName = "Campanha",
                            requesterId = Guid.NewGuid(),
                            requesterName = "Carla Dias",
                            overdueSince
                        }
                    },
                    page = 1,
                    pageSize = 20,
                    totalCount = 2
                },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage TrackingResponse(Guid id) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id,
                    title = "Campanha",
                    objective = (string?)null,
                    categoryId = Guid.NewGuid(),
                    desiredDeliveryDate = (DateOnly?)null,
                    priority = (int)Priority.Medium,
                    status = (int)BriefingStatus.InProgress,
                    requesterId = Guid.NewGuid(),
                    requesterName = "Clara Solicitante",
                    progress = new { totalTasks = 4, approvedTasks = 2, percentage = 50m, isComplete = false },
                    tasks = new[]
                    {
                        new
                        {
                            id = Guid.NewGuid(),
                            title = "Banner topo",
                            designerId = (Guid?)null,
                            designerName = (string?)null,
                            deadline = (DateOnly?)null,
                            priority = (int)Priority.High,
                            status = (int)BriefingTaskStatus.InProgress
                        }
                    }
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
    public async Task GetBriefingTrackingAsync_WhenOk_ReturnsProgressAndTasks()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => TrackingResponse(id));

        // Act
        var result = await client.GetBriefingTrackingAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.Progress.ApprovedTasks.Should().Be(2);
        result.Value.Progress.TotalTasks.Should().Be(4);
        result.Value.Progress.Percentage.Should().Be(50m);
        result.Value.Tasks.Should().ContainSingle().Which.Title.Should().Be("Banner topo");
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/tracking");
    }

    [Fact]
    public async Task GetBriefingTrackingAsync_WhenForbidden_MapsToForbiddenFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.GetBriefingTrackingAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetBriefingTrackingAsync_WhenNotFound_MapsToNotFoundFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await client.GetBriefingTrackingAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
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
            Content = BriefingResponse(id, BriefingStatus.AwaitingApproval).Content
        });

        // Act
        var result = await client.SubmitBriefingAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.AwaitingApproval);
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
        await client.GetBriefingsAsync(new BriefingListQuery(1, 20, BriefingStatus.AwaitingApproval, categoryId));

        // Assert
        var query = handler.Requests[0].RequestUri!.Query;
        query.Should().Contain($"status={nameof(BriefingStatus.AwaitingApproval)}");
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
    public async Task GetPendingApprovalBriefingsAsync_WhenOk_ReturnsQueueWithResolvedNames()
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
                            categoryName = "Campanha de Marketing",
                            requesterId = Guid.NewGuid(),
                            requesterName = "Ana Souza",
                            status = (int)BriefingStatus.AwaitingApproval,
                            createdAt = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc)
                        }
                    },
                    page = 1,
                    pageSize = 20,
                    totalCount = 1
                },
                options: WireJsonOptions)
        });

        // Act
        var result = await client.GetPendingApprovalBriefingsAsync(1, 20);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(id);
        item.RequesterName.Should().Be("Ana Souza");
        item.CategoryName.Should().Be("Campanha de Marketing");
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/briefings/pending-approval");
    }

    [Fact]
    public async Task GetPendingApprovalBriefingsAsync_Always_SendsPagination()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedBriefingsResponse());

        // Act
        await client.GetPendingApprovalBriefingsAsync(3, 50);

        // Assert
        handler.Requests[0].RequestUri!.Query.Should().Be("?page=3&pageSize=50");
    }

    [Fact]
    public async Task GetPendingApprovalBriefingsAsync_WhenForbidden_MapsToForbiddenFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.GetPendingApprovalBriefingsAsync(1, 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task ApproveBriefingAsync_PostsToApproveEndpoint()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = BriefingResponse(id, BriefingStatus.Approved).Content
        });

        // Act
        var result = await client.ApproveBriefingAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Approved);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/approve");
    }

    [Fact]
    public async Task ApproveBriefingAsync_WhenBriefingLeftAwaitingApproval_MapsToConflictFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

        // Act
        var result = await client.ApproveBriefingAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task RejectBriefingAsync_PostsJustificationToRejectEndpoint()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = BriefingResponse(id, BriefingStatus.Rejected).Content
        });

        // Act
        var result = await client.RejectBriefingAsync(id, "Orçamento indisponível.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Rejected);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/reject");
        var body = await handler.Requests[0].Content!.ReadFromJsonAsync<RejectBriefingRequest>(WireJsonOptions);
        body!.Justification.Should().Be("Orçamento indisponível.");
    }

    [Fact]
    public async Task RequestChangesBriefingAsync_PostsJustificationToRequestChangesEndpoint()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = BriefingResponse(id, BriefingStatus.ChangesRequested).Content
        });

        // Act
        var result = await client.RequestChangesBriefingAsync(id, "Faltou o público-alvo.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.ChangesRequested);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/briefings/{id}/request-changes");
        var body = await handler.Requests[0].Content!.ReadFromJsonAsync<RequestChangesBriefingRequest>(WireJsonOptions);
        body!.Justification.Should().Be("Faltou o público-alvo.");
    }

    [Fact]
    public async Task RejectBriefingAsync_WhenJustificationIsRefusedByTheApi_ReturnsFieldKeyedFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(
                new
                {
                    title = "Falha ao rejeitar briefing",
                    status = 400,
                    errors = new Dictionary<string, string[]> { ["Justification"] = ["Justification is required."] }
                },
                options: WireJsonOptions)
        });

        // Act
        var result = await client.RejectBriefingAsync(Guid.NewGuid(), " ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Justification");
    }

    [Fact]
    public async Task GetOverdueBriefingsAsync_WhenOk_ReturnsRowsAndTotalCount()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var overdueSince = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        var (client, handler) = CreateSut(_ => OverdueBriefingsResponse(briefingId, overdueSince));

        // Act
        var result = await client.GetOverdueBriefingsAsync(page: 1, pageSize: 20);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        var row = result.Value.Items.Should().ContainSingle().Subject;
        row.Id.Should().Be(briefingId);
        row.RequesterName.Should().Be("Carla Dias");
        row.CategoryName.Should().Be("Campanha");
        row.OverdueSince.Should().Be(overdueSince);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/briefings/overdue");
    }

    [Fact]
    public async Task GetOverdueBriefingsAsync_WhenCalled_SendsRequestedPage()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => OverdueBriefingsResponse(Guid.NewGuid(), DateTime.UtcNow));

        // Act
        await client.GetOverdueBriefingsAsync(page: 2, pageSize: 5);

        // Assert
        handler.Requests[0].RequestUri!.Query.Should().Contain("page=2").And.Contain("pageSize=5");
    }

    [Fact]
    public async Task GetOverdueBriefingsAsync_WhenForbidden_MapsToForbiddenFailure()
    {
        // Arrange — the caller holds briefings.view-own only; the overdue list needs briefings.view-all.
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.GetOverdueBriefingsAsync(page: 1, pageSize: 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetOverdueBriefingsAsync_WhenTransportFails_ReturnsFailure()
    {
        // Arrange — the panel must surface a message and keep the dashboard's other half rendered.
        var (client, _) = CreateSut(_ => throw new HttpRequestException("network down"));

        // Act
        var result = await client.GetOverdueBriefingsAsync(page: 1, pageSize: 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
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
}
