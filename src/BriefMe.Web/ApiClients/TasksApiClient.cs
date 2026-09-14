using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.Contracts.Common;
using BriefMe.Web.Contracts.Tasks;
using BriefMe.Web.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Typed client for the Designer's task board (BRF-033). Sends through <see cref="AuthenticatedApiClient"/>
/// (the "Api" pipeline: bearer + refresh-on-401) and maps non-success responses via
/// <see cref="ApiProblemMapper"/>, so the screen surfaces backend failures uniformly.
/// </summary>
public sealed class TasksApiClient : ITasksApiClient
{
    private const string LoadTasksFailureMessage = "Não foi possível carregar as tarefas. Tente novamente.";
    private const string LoadTaskFailureMessage = "Não foi possível carregar a tarefa. Tente novamente.";
    private const string LoadOverdueTasksFailureMessage = "Não foi possível carregar as tarefas atrasadas. Tente novamente.";
    private const string UpdateStatusFailureMessage = "Não foi possível atualizar o status da tarefa. Tente novamente.";
    private const string RegisterCommentFailureMessage = "Não foi possível registrar o comentário. Tente novamente.";
    private const string SubmitDeliveryFailureMessage = "Não foi possível enviar o arquivo. Tente novamente.";

    private static readonly JsonSerializerOptions ResponseJsonOptions = ApiProblemMapper.JsonOptions;

    private readonly AuthenticatedApiClient _api;

    public TasksApiClient(AuthenticatedApiClient api) => _api = api;

    public async Task<Result<PagedResponse<DesignerTaskDto>>> GetMyTasksAsync(
        TaskListQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync(BuildTasksRequestUri(query), cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<PagedResponse<DesignerTaskDto>>(response, LoadTasksFailureMessage, cancellationToken: cancellationToken);

            var page = await response.Content.ReadFromJsonAsync<PagedResponse<DesignerTaskDto>>(ResponseJsonOptions, cancellationToken);

            return page is null
                ? Result.Failure<PagedResponse<DesignerTaskDto>>(LoadTasksFailureMessage)
                : Result.Success(page);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<DesignerTaskDto>>(LoadTasksFailureMessage);
        }
    }

    public async Task<Result<PagedResponse<OverdueTaskDto>>> GetOverdueTasksAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync(BuildPagedRequestUri("tasks/overdue", page, pageSize), cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<PagedResponse<OverdueTaskDto>>(response, LoadOverdueTasksFailureMessage, cancellationToken: cancellationToken);

            var overduePage = await response.Content.ReadFromJsonAsync<PagedResponse<OverdueTaskDto>>(ResponseJsonOptions, cancellationToken);

            return overduePage is null
                ? Result.Failure<PagedResponse<OverdueTaskDto>>(LoadOverdueTasksFailureMessage)
                : Result.Success(overduePage);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<OverdueTaskDto>>(LoadOverdueTasksFailureMessage);
        }
    }

    public async Task<Result<TaskDetailDto>> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync($"tasks/{taskId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<TaskDetailDto>(response, LoadTaskFailureMessage, cancellationToken: cancellationToken);

            var task = await response.Content.ReadFromJsonAsync<TaskDetailDto>(ResponseJsonOptions, cancellationToken);

            return task is null
                ? Result.Failure<TaskDetailDto>(LoadTaskFailureMessage)
                : Result.Success(task);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<TaskDetailDto>(LoadTaskFailureMessage);
        }
    }

    public async Task<Result<CommentDto>> RegisterCommentAsync(
        Guid taskId,
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PostAsJsonAsync(
                $"tasks/{taskId}/comments",
                new RegisterCommentRequest(text),
                ResponseJsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<CommentDto>(response, RegisterCommentFailureMessage, cancellationToken: cancellationToken);

            var comment = await response.Content.ReadFromJsonAsync<CommentDto>(ResponseJsonOptions, cancellationToken);

            return comment is null
                ? Result.Failure<CommentDto>(RegisterCommentFailureMessage)
                : Result.Success(comment);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<CommentDto>(RegisterCommentFailureMessage);
        }
    }

    public async Task<Result<AttachmentDto>> SubmitDeliveryAsync(
        Guid taskId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Rebuilt per attempt: the 401 retry sends a second request, and a consumed HttpContent cannot
            // be sent twice. The part is named "file" to bind to the controller's IFormFile parameter.
            using var response = await _api.PostAsync(
                $"tasks/{taskId}/deliveries",
                () => new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<AttachmentDto>(response, SubmitDeliveryFailureMessage, cancellationToken: cancellationToken);

            var attachment = await response.Content.ReadFromJsonAsync<AttachmentDto>(ResponseJsonOptions, cancellationToken);

            return attachment is null
                ? Result.Failure<AttachmentDto>(SubmitDeliveryFailureMessage)
                : Result.Success(attachment);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<AttachmentDto>(SubmitDeliveryFailureMessage);
        }
    }

    public async Task<Result<BriefingTaskDto>> UpdateTaskStatusAsync(
        Guid taskId,
        BriefingTaskStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PutAsJsonAsync(
                $"tasks/{taskId}/status",
                new UpdateTaskStatusRequest(status),
                ResponseJsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<BriefingTaskDto>(response, UpdateStatusFailureMessage, cancellationToken: cancellationToken);

            var task = await response.Content.ReadFromJsonAsync<BriefingTaskDto>(ResponseJsonOptions, cancellationToken);

            return task is null
                ? Result.Failure<BriefingTaskDto>(UpdateStatusFailureMessage)
                : Result.Success(task);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<BriefingTaskDto>(UpdateStatusFailureMessage);
        }
    }

    private static string BuildTasksRequestUri(TaskListQuery query)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
        };

        if (query.Status is { } status)
            parameters["status"] = status.ToString();

        if (query.Priority is { } priority)
            parameters["priority"] = priority.ToString();

        return QueryHelpers.AddQueryString("tasks", parameters);
    }

    /// <summary>Adds the paging pair to a route that takes no other parameter (the overdue list, BRF-046).</summary>
    private static string BuildPagedRequestUri(string route, int page, int pageSize) =>
        QueryHelpers.AddQueryString(route, new Dictionary<string, string?>
        {
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
        });
}
