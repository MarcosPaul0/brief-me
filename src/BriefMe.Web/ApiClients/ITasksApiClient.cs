using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.Contracts.Common;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Thin typed client the Blazor task screens use to reach the REST API (BRF-033) — keeps HTTP concerns out
/// of the Razor components (CLAUDE.md: components stay thin). Runs over the <c>"Api"</c> HttpClient, so
/// bearer-token attachment and refresh-on-401 come for free, and surfaces the API's failures as a failed
/// <see cref="Result{T}"/>.
/// </summary>
public interface ITasksApiClient
{
    /// <summary>
    /// Lists the authenticated Designer's own tasks via <c>GET /tasks</c> (BRF-033, RF017–RF020),
    /// paginated and optionally filtered by status/priority. Requires <c>tasks.view-assigned</c>.
    /// </summary>
    Task<Result<PagedResponse<DesignerTaskDto>>> GetMyTasksAsync(
        TaskListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the tasks past their deadline via <c>GET /tasks/overdue</c> (BRF-046, RF029), paginated,
    /// longest late first — the task half of the SLA dashboard. Requires <c>tasks.view-all</c>: the list
    /// crosses every designer's workload, so <c>tasks.view-assigned</c> comes back as
    /// <see cref="ErrorType.Forbidden"/>. Takes no filter — the query has none, by BRF-044's decision.
    /// </summary>
    Task<Result<PagedResponse<OverdueTaskDto>>> GetOverdueTasksAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one task with its comments and deliveries via <c>GET /tasks/{id}</c> (BRF-034, RF021, RF022,
    /// RF052) — the detail screen's only read. Requires <c>tasks.view-assigned</c> or <c>tasks.view-all</c>;
    /// a caller not involved in the task comes back as <see cref="ErrorType.Forbidden"/> and an unknown id
    /// as <see cref="ErrorType.NotFound"/>.
    /// </summary>
    Task<Result<TaskDetailDto>> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a comment on a task via <c>POST /tasks/{id}/comments</c> (BRF-030, RF021). Requires
    /// <c>tasks.comment</c>; empty or oversized text comes back as validation errors.
    /// </summary>
    Task<Result<CommentDto>> RegisterCommentAsync(
        Guid taskId,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a delivered file via <c>POST /tasks/{id}/deliveries</c> as <c>multipart/form-data</c>
    /// (BRF-031, RF022). Takes the bytes already read from the picked file: the request may be replayed
    /// after a token refresh, so the body has to be reproducible. Size and extension limits are the
    /// server's (<c>IFileValidator</c>, BRF-011) and surface as validation errors.
    /// </summary>
    Task<Result<AttachmentDto>> SubmitDeliveryAsync(
        Guid taskId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a task through the execution states of RF020 via <c>PUT /tasks/{id}/status</c> (BRF-029).
    /// A transition the task's current state does not allow — a stale board — comes back as a
    /// <see cref="ErrorType.Conflict"/> failure; a caller who is neither the assignee nor a manager gets
    /// <see cref="ErrorType.Forbidden"/>.
    /// </summary>
    Task<Result<BriefingTaskDto>> UpdateTaskStatusAsync(
        Guid taskId,
        BriefingTaskStatus status,
        CancellationToken cancellationToken = default);
}
