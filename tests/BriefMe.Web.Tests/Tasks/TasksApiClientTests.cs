using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.ApiClients;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.Tasks;

public class TasksApiClientTests
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

    private static (TasksApiClient Client, RoutedStubHandler Handler) CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var client = new TasksApiClient(AuthTestHelpers.CreateApiClient(httpClient));
        return (client, handler);
    }

    private static HttpResponseMessage PagedTasksResponse(Guid taskId, string? briefingTitle) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new
                        {
                            id = taskId,
                            briefingId = Guid.NewGuid(),
                            briefingTitle,
                            title = "Banner topo",
                            deadline = (DateOnly?)null,
                            priority = (int)Priority.High,
                            status = (int)BriefingTaskStatus.Blocked
                        }
                    },
                    page = 1,
                    pageSize = 20,
                    totalCount = 1
                },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage OverdueTasksResponse(Guid taskId, DateTime overdueSince) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new
                        {
                            id = taskId,
                            briefingId = Guid.NewGuid(),
                            briefingTitle = "Campanha de verão",
                            title = "Banner topo",
                            deadline = (DateOnly?)null,
                            priority = (int)Priority.High,
                            status = (int)BriefingTaskStatus.InProgress,
                            designerId = Guid.NewGuid(),
                            designerName = "Ana Souza",
                            overdueSince
                        }
                    },
                    page = 1,
                    pageSize = 20,
                    totalCount = 3
                },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage TaskResponse(Guid taskId, BriefingTaskStatus status) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id = taskId,
                    briefingId = Guid.NewGuid(),
                    title = "Banner topo",
                    designerId = Guid.NewGuid(),
                    deadline = (DateOnly?)null,
                    priority = (int)Priority.High,
                    status = (int)status
                },
                options: WireJsonOptions)
        };

    [Fact]
    public async Task GetMyTasksAsync_WhenOk_ReturnsPagedTasks()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => PagedTasksResponse(taskId, "Campanha de verão"));

        // Act
        var result = await client.GetMyTasksAsync(new TaskListQuery(Page: 1, PageSize: 20));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(taskId);
        item.BriefingTitle.Should().Be("Campanha de verão");
        item.Status.Should().Be(BriefingTaskStatus.Blocked);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/tasks");
    }

    [Fact]
    public async Task GetMyTasksAsync_WhenBriefingTitleIsNull_KeepsTheRow()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var (client, _) = CreateSut(_ => PagedTasksResponse(taskId, null));

        // Act
        var result = await client.GetMyTasksAsync(new TaskListQuery(Page: 1, PageSize: 20));

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(taskId);
        item.BriefingTitle.Should().BeNull();
    }

    [Fact]
    public async Task GetMyTasksAsync_SendsPagingQueryString()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedTasksResponse(Guid.NewGuid(), null));

        // Act
        await client.GetMyTasksAsync(new TaskListQuery(Page: 3, PageSize: 50));

        // Assert
        handler.Requests[0].RequestUri!.Query.Should().Contain("page=3").And.Contain("pageSize=50");
    }

    [Fact]
    public async Task GetMyTasksAsync_WhenFiltersAreSet_SendsThemInQueryString()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedTasksResponse(Guid.NewGuid(), null));

        var query = new TaskListQuery(
            Page: 1,
            PageSize: 20,
            Status: BriefingTaskStatus.Blocked,
            Priority: Priority.Urgent);

        // Act
        await client.GetMyTasksAsync(query);

        // Assert
        handler.Requests[0].RequestUri!.Query
            .Should().Contain("status=Blocked")
            .And.Contain("priority=Urgent");
    }

    [Fact]
    public async Task GetMyTasksAsync_WhenFiltersAreAbsent_OmitsThemFromQueryString()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => PagedTasksResponse(Guid.NewGuid(), null));

        // Act
        await client.GetMyTasksAsync(new TaskListQuery(Page: 1, PageSize: 20));

        // Assert
        handler.Requests[0].RequestUri!.Query
            .Should().NotContain("status=")
            .And.NotContain("priority=");
    }

    [Fact]
    public async Task GetMyTasksAsync_WhenRequestFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await client.GetMyTasksAsync(new TaskListQuery(Page: 1, PageSize: 20));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_PutsToStatusEndpoint()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => TaskResponse(taskId, BriefingTaskStatus.InProgress));

        // Act
        var result = await client.UpdateTaskStatusAsync(taskId, BriefingTaskStatus.InProgress);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingTaskStatus.InProgress);
        handler.Requests[0].Method.Should().Be(HttpMethod.Put);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/tasks/{taskId}/status");
    }

    private static HttpResponseMessage TaskDetailResponse(Guid taskId, string? authorName, string? uploadedByName) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id = taskId,
                    briefingId = Guid.NewGuid(),
                    briefingTitle = "Campanha de verão",
                    title = "Banner topo",
                    designerId = Guid.NewGuid(),
                    designerName = "Ana Designer",
                    deadline = (DateOnly?)null,
                    priority = (int)Priority.High,
                    status = (int)BriefingTaskStatus.InProgress,
                    comments = new[]
                    {
                        new
                        {
                            id = Guid.NewGuid(),
                            authorId = Guid.NewGuid(),
                            authorName,
                            text = "Primeira versão enviada.",
                            createdAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
                        }
                    },
                    deliveries = new[]
                    {
                        new
                        {
                            id = Guid.NewGuid(),
                            fileName = "banner.png",
                            sizeBytes = 2048L,
                            uploadedById = Guid.NewGuid(),
                            uploadedByName,
                            uploadedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc)
                        }
                    }
                },
                options: WireJsonOptions)
        };

    [Fact]
    public async Task GetTaskAsync_GetsFromTaskEndpointAndReadsCommentsAndDeliveries()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => TaskDetailResponse(taskId, "Ana Designer", "Ana Designer"));

        // Act
        var result = await client.GetTaskAsync(taskId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(taskId);
        result.Value.BriefingTitle.Should().Be("Campanha de verão");
        result.Value.Comments.Should().ContainSingle().Which.AuthorName.Should().Be("Ana Designer");
        // RF052: the delivery arrives with its author and moment.
        var delivery = result.Value.Deliveries.Should().ContainSingle().Subject;
        delivery.FileName.Should().Be("banner.png");
        delivery.UploadedByName.Should().Be("Ana Designer");
        delivery.UploadedAt.Should().Be(new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc));
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/tasks/{taskId}");
    }

    [Fact]
    public async Task GetTaskAsync_WhenNamesAreNull_KeepsTheRows()
    {
        // Arrange
        var (client, _) = CreateSut(_ => TaskDetailResponse(Guid.NewGuid(), null, null));

        // Act
        var result = await client.GetTaskAsync(Guid.NewGuid());

        // Assert
        result.Value.Comments.Should().ContainSingle().Which.AuthorName.Should().BeNull();
        result.Value.Deliveries.Should().ContainSingle().Which.UploadedByName.Should().BeNull();
    }

    [Fact]
    public async Task GetTaskAsync_WhenTaskIsNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await client.GetTaskAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetTaskAsync_WhenCallerIsNotInvolved_ReturnsForbiddenFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.GetTaskAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task RegisterCommentAsync_PostsToCommentsEndpoint()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id = Guid.NewGuid(),
                    taskId,
                    authorId = Guid.NewGuid(),
                    text = "Primeira versão enviada.",
                    createdAt = DateTime.UtcNow
                },
                options: WireJsonOptions)
        });

        // Act
        var result = await client.RegisterCommentAsync(taskId, "Primeira versão enviada.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Primeira versão enviada.");
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/tasks/{taskId}/comments");
    }

    [Fact]
    public async Task RegisterCommentAsync_WhenRequestFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.RegisterCommentAsync(Guid.NewGuid(), "Texto");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task SubmitDeliveryAsync_PostsMultipartToDeliveriesEndpoint()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id = Guid.NewGuid(),
                    briefingId = Guid.NewGuid(),
                    taskId,
                    fileName = "banner.png",
                    sizeBytes = 3L,
                    uploadedById = Guid.NewGuid(),
                    uploadedAt = DateTime.UtcNow
                },
                options: WireJsonOptions)
        });

        // Act
        var result = await client.SubmitDeliveryAsync(taskId, "banner.png", [1, 2, 3]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("banner.png");
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/tasks/{taskId}/deliveries");
        handler.Requests[0].Content.Should().BeOfType<MultipartFormDataContent>();
    }

    [Fact]
    public async Task SubmitDeliveryAsync_WhenRequestFails_ReturnsFailure()
    {
        // Arrange — the server refused the file (size/extension, IFileValidator — BRF-011).
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        // Act
        var result = await client.SubmitDeliveryAsync(Guid.NewGuid(), "banner.exe", [1]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_WhenTransitionIsRejected_ReturnsFailure()
    {
        // Arrange — a stale board: the task moved on server-side, so the handler answers 409 (BRF-029).
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

        // Act
        var result = await client.UpdateTaskStatusAsync(Guid.NewGuid(), BriefingTaskStatus.InProgress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetOverdueTasksAsync_WhenOk_ReturnsRowsAndTotalCount()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var overdueSince = new DateTime(2026, 7, 30, 3, 0, 0, DateTimeKind.Utc);
        var (client, handler) = CreateSut(_ => OverdueTasksResponse(taskId, overdueSince));

        // Act
        var result = await client.GetOverdueTasksAsync(page: 1, pageSize: 20);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        var row = result.Value.Items.Should().ContainSingle().Subject;
        row.Id.Should().Be(taskId);
        row.DesignerName.Should().Be("Ana Souza");
        row.OverdueSince.Should().Be(overdueSince);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/tasks/overdue");
    }

    [Fact]
    public async Task GetOverdueTasksAsync_WhenCalled_SendsRequestedPage()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => OverdueTasksResponse(Guid.NewGuid(), DateTime.UtcNow));

        // Act
        await client.GetOverdueTasksAsync(page: 3, pageSize: 50);

        // Assert
        handler.Requests[0].RequestUri!.Query.Should().Contain("page=3").And.Contain("pageSize=50");
    }

    [Fact]
    public async Task GetOverdueTasksAsync_WhenForbidden_MapsToForbiddenFailure()
    {
        // Arrange — the caller holds tasks.view-assigned only; the overdue list needs tasks.view-all.
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var result = await client.GetOverdueTasksAsync(page: 1, pageSize: 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetOverdueTasksAsync_WhenTransportFails_ReturnsFailure()
    {
        // Arrange — the panel must surface a message and keep the dashboard's other half rendered.
        var (client, _) = CreateSut(_ => throw new HttpRequestException("network down"));

        // Act
        var result = await client.GetOverdueTasksAsync(page: 1, pageSize: 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }
}
