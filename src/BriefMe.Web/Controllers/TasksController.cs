using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.BriefingTasks.AssignTask;
using BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;
using BriefMe.Application.UseCases.BriefingTasks.GetTaskDetail;
using BriefMe.Application.UseCases.BriefingTasks.GetTasksByDesigner;
using BriefMe.Application.UseCases.BriefingTasks.ReassignTask;
using BriefMe.Application.UseCases.BriefingTasks.RegisterComment;
using BriefMe.Application.UseCases.BriefingTasks.ReopenTask;
using BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;
using BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;
using BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;
using BriefMe.Application.UseCases.Sla.GetOverdueTasks;
using BriefMe.Domain.Constants;
using BriefMe.Domain.Enums;
using BriefMe.Web.Authorization;
using BriefMe.Web.Contracts.Tasks;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// Exposes the task use cases over HTTP (BRF-032, RF015–RF022). A thin controller: it only translates
/// request → command, dispatches via <see cref="ISender"/> and maps <c>Result&lt;T&gt;</c> to the HTTP
/// response.
/// </summary>
/// <remarks>
/// The permission gate here decides whether the caller may perform the action at all. Whether they may act
/// on <b>this particular</b> task — the assignee of RF020, "the involved" of RF021, the assigned designer of
/// RF022, plus the management override each of those handlers grants — is data, not a permission, and stays
/// in the handlers, which answer with <c>Result.Forbidden</c> and surface here as a <c>403</c>.
/// <para>
/// Creating endpoints answer <c>200 OK</c> rather than <c>201 Created</c>: task creation is a bulk act
/// (RF015) with no single resource to point at, and comments and deliveries have no route of their own —
/// they are read as part of the task detail (BRF-034) and downloaded by attachment id later (BRF-077), so a
/// <c>Location</c> header would point at nothing.
/// </para>
/// </remarks>
[ApiController]
[Route("tasks")]
[Authorize]
public sealed class TasksController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Lists the authenticated Designer's own tasks (BRF-033), paginated and optionally filtered by status
    /// and priority — the read behind the task board. Ordering is the repository's: nearest deadline first,
    /// undated last (BRF-025).
    /// </summary>
    /// <remarks>
    /// Assignee-scoping is not a permission, so <c>tasks.view-assigned</c> only opens the door; the query
    /// itself reads the designer from <c>ICurrentUser</c> and no caller can widen it. The Design Manager's
    /// view of every task (<c>tasks.view-all</c>) is a different read and is not this route.
    /// </remarks>
    [HttpGet]
    [HasPermission(Permissions.Tasks.ViewAssigned)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] BriefingTaskStatus? status = null,
        [FromQuery] Priority? priority = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTasksByDesignerQuery(page, pageSize, status, priority);

        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar as tarefas");
    }

    /// <summary>
    /// Lists the tasks currently past their deadline (RF029, BRF-044), paginated, longest late first — the
    /// read behind the SLA dashboard (BRF-046). Each row carries the deadline that was broken, the assignee
    /// and the briefing it serves.
    /// </summary>
    /// <remarks>
    /// Gated on <c>tasks.view-all</c> alone: the list crosses every designer's workload, which is exactly
    /// what <c>tasks.view-assigned</c> does not open. Whether a task is late is not decided here — the SLA
    /// monitor (BRF-043) marked it, and this route reports the mark.
    /// <para>
    /// Declared before <c>GET /tasks/{id}</c> for readability only: the <c>:guid</c> constraint on that
    /// route cannot match <c>overdue</c>, so the two never compete.
    /// </para>
    /// </remarks>
    [HttpGet("overdue")]
    [HasPermission(Permissions.Tasks.ViewAll)]
    public async Task<IActionResult> GetOverdue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetOverdueTasksQuery(page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar as tarefas atrasadas");
    }

    /// <summary>
    /// Reads one task with its comment trail (RF021) and the files delivered on it, each carrying the
    /// author and moment of RF052 (BRF-034) — the record behind a row of the board.
    /// </summary>
    /// <remarks>
    /// Gated on <b>either</b> task-view permission: the assignee reaches it through
    /// <c>tasks.view-assigned</c> and design management through <c>tasks.view-all</c>, and neither implies
    /// the other in the seeded catalog (USR-047). Holding one only opens the door — whether the caller is
    /// involved in <b>this</b> task is data the handler decides, answering <c>403</c> when they are not.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Tasks.ViewAssigned, Permissions.Tasks.ViewAll)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTaskDetailQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar a tarefa");
    }

    /// <summary>
    /// Breaks an approved briefing down into its tasks (RF015, RF016). Nested under the briefing because a
    /// task belongs to exactly one, but implemented here because the Tasks module owns the use case — the
    /// absolute route escapes this controller's <c>tasks</c> prefix.
    /// </summary>
    [HttpPost("/briefings/{briefingId:guid}/tasks")]
    [HasPermission(Permissions.Tasks.Create)]
    public async Task<IActionResult> Create(
        Guid briefingId,
        CreateBriefingTasksRequest request,
        CancellationToken cancellationToken)
    {
        var items = request.Tasks
            .Select(task => new CreateBriefingTaskItem(task.Title, task.Priority))
            .ToList();

        var command = new CreateBriefingTasksCommand(briefingId, items);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao criar tarefas do briefing");
    }

    /// <summary>Gives a task its assignee (RF017).</summary>
    [HttpPut("{id:guid}/assign")]
    [HasPermission(Permissions.Tasks.Assign)]
    public async Task<IActionResult> Assign(Guid id, AssignTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AssignTaskCommand(id, request.DesignerId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atribuir tarefa");
    }

    /// <summary>
    /// Hands an assigned task to a different Designer (RF026), raising <c>TaskAssigned</c> for the new owner.
    /// </summary>
    /// <remarks>
    /// A route of its own rather than a second meaning for <c>/assign</c>: the two acts carry different
    /// permissions, and <c>tasks.reassign</c> is what RF026 reserves to the Design Manager — holding
    /// <c>tasks.assign</c> does not open this one. <c>PUT</c> because the transfer carries its destination in
    /// the body, unlike the payload-free <c>/reopen</c>.
    /// </remarks>
    [HttpPut("{id:guid}/reassign")]
    [HasPermission(Permissions.Tasks.Reassign)]
    public async Task<IActionResult> Reassign(
        Guid id,
        ReassignTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReassignTaskCommand(id, request.DesignerId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao reatribuir tarefa");
    }

    /// <summary>
    /// Plans a task: deadline (RF018) and priority (RF019). Gated on <c>tasks.create</c> — the catalog has no
    /// planning permission, and planning is part of the same decomposition act the Design Manager performs in
    /// RF015.
    /// </summary>
    [HttpPut("{id:guid}/deadline-priority")]
    [HasPermission(Permissions.Tasks.Create)]
    public async Task<IActionResult> SetDeadlineAndPriority(
        Guid id,
        SetDeadlinePriorityRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetDeadlinePriorityCommand(id, request.Deadline, request.Priority);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao definir prazo e prioridade da tarefa");
    }

    /// <summary>Moves a task through the execution states of RF020.</summary>
    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.Tasks.UpdateStatus)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateTaskStatusCommand(id, request.Status), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar status da tarefa");
    }

    /// <summary>
    /// Reopens a task the requester had already approved (RF025), bringing its briefing back to execution
    /// when that task was the last pending one (RF058).
    /// </summary>
    /// <remarks>
    /// <c>POST</c> without a body, like the state actions of <c>BriefingsController</c>: reopening carries no
    /// payload — RF025 asks for no justification — so there is nothing to <c>PUT</c>. Gated on
    /// <c>tasks.reopen</c>, which the seed (USR-047) gives to the Design Manager and the Administrator and
    /// deliberately not to the Designer: the assignee must not undo the requester's approval of their own
    /// delivery.
    /// </remarks>
    [HttpPost("{id:guid}/reopen")]
    [HasPermission(Permissions.Tasks.Reopen)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReopenTaskCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao reabrir a tarefa");
    }

    /// <summary>Records a comment on a task (RF021).</summary>
    [HttpPost("{id:guid}/comments")]
    [HasPermission(Permissions.Tasks.Comment)]
    public async Task<IActionResult> RegisterComment(
        Guid id,
        RegisterCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterCommentCommand(id, request.Text), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao registrar comentário na tarefa");
    }

    /// <summary>
    /// Attaches a file delivered on a task (RF022). Consumes <c>multipart/form-data</c> with a single
    /// <c>file</c> part and streams it into the command — <c>IFileStorageService</c> takes a
    /// <see cref="Stream"/> (BRF-011), so buffering the upload here would buy nothing.
    /// </summary>
    /// <remarks>
    /// A missing part is deliberately not short-circuited: the command is dispatched with the nulls and
    /// <c>SubmitTaskDeliveryCommandValidator</c> turns them into a <c>400</c>, keeping the wording in the
    /// <c>BriefingTaskErrors.Validation</c> catalog instead of restating it in Web. Size and extension limits
    /// belong to <c>IFileValidator</c>, which reads them from configuration.
    /// </remarks>
    [HttpPost("{id:guid}/deliveries")]
    [HasPermission(Permissions.Tasks.Deliver)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitDelivery(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        // Disposed once the handler has copied it into storage; the request would release it anyway, but the
        // scope makes the lifetime explicit.
        await using var content = file?.OpenReadStream();

        var command = new SubmitTaskDeliveryCommand(id, file?.FileName, file?.Length ?? 0, content);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao registrar entrega da tarefa");
    }
}
