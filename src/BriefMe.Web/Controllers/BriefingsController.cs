using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Briefings.ApproveBriefing;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;
using BriefMe.Application.UseCases.Briefings.GetBriefingTracking;
using BriefMe.Application.UseCases.Briefings.RejectBriefing;
using BriefMe.Application.UseCases.Briefings.RemoveBriefingAttachment;
using BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;
using BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;
using BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;
using BriefMe.Application.UseCases.Sla.GetOverdueBriefings;
using BriefMe.Domain.Constants;
using BriefMe.Domain.Enums;
using BriefMe.Web.Authorization;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// Exposes the briefing use cases over HTTP (BRF-012, RF004–RF010) plus the review transitions
/// approve/reject/request-changes (BRF-019, RF011–RF014). A thin controller: it only translates
/// request → command/query, dispatches via <see cref="ISender"/> and maps <c>Result&lt;T&gt;</c> to the HTTP
/// response. Ownership and state invariants live in the handlers; this layer only gates on permissions.
/// </summary>
[ApiController]
[Route("briefings")]
[Authorize]
public sealed class BriefingsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.Briefings.Create)]
    public async Task<IActionResult> Create(CreateBriefingRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateBriefingCommand(
            request.Title,
            request.Priority,
            request.CategoryId,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Created($"/briefings/{result.Value.Id}", result.Value)
            : result.ToProblem(this, "Falha ao criar briefing");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Briefings.Create)]
    public async Task<IActionResult> Update(Guid id, UpdateBriefingRequest request, CancellationToken cancellationToken)
    {
        var command = new EditBriefingCommand(
            id,
            request.Title,
            request.Priority,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao editar briefing");
    }

    [HttpPost("{id:guid}/submit")]
    [HasPermission(Permissions.Briefings.Create)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitBriefingForApprovalCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao submeter briefing para aprovação");
    }

    /// <summary>
    /// Attaches a reference file to a briefing (RF005). Consumes <c>multipart/form-data</c> with a single
    /// <c>file</c> part and streams it into the command — <c>IFileStorageService</c> takes a
    /// <see cref="Stream"/> (BRF-011), so buffering the upload here would buy nothing.
    /// </summary>
    /// <remarks>
    /// A missing part is deliberately not short-circuited: the command is dispatched with the nulls and
    /// <c>UploadBriefingAttachmentCommandValidator</c> turns them into a <c>400</c>, keeping the wording in
    /// the <c>BriefingErrors.Validation</c> catalog instead of restating it in Web. Size and extension limits
    /// belong to <c>IFileValidator</c>, which reads them from configuration.
    /// <para>
    /// Gated on <c>briefings.create</c>, like <c>PUT /briefings/{id}</c>: attaching material is part of
    /// composing a briefing. Whether the caller owns <b>this</b> one is the handler's answer (<c>403</c>).
    /// </para>
    /// </remarks>
    [HttpPost("{id:guid}/attachments")]
    [HasPermission(Permissions.Briefings.Create)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        // Disposed once the handler has copied it into storage; the request would release it anyway, but the
        // scope makes the lifetime explicit.
        await using var content = file?.OpenReadStream();

        var command = new UploadBriefingAttachmentCommand(id, file?.FileName, file?.Length ?? 0, content);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao anexar arquivo ao briefing");
    }

    /// <summary>
    /// Removes one reference file from a briefing (RF005) and returns the briefing with the files that
    /// remain, so the form has nothing left to re-fetch.
    /// </summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(Permissions.Briefings.Create)]
    public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveBriefingAttachmentCommand(id, attachmentId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao remover o anexo do briefing");
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Briefings.Create)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelBriefingCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao cancelar briefing");
    }

    /// <summary>
    /// Approves a briefing under review (BRF-019, RF011). Reviewing is an act of a third party over someone
    /// else's briefing, so there is no ownership check anywhere in this path — the permission gate is the
    /// only authorization layer that applies.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Briefings.Approve)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveBriefingCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao aprovar briefing");
    }

    /// <summary>
    /// Rejects a briefing under review (BRF-019, RF012). The justification is mandatory (RF013) and is
    /// validated by <c>RejectBriefingCommandValidator</c>, which surfaces a missing reason as a 400.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [HasPermission(Permissions.Briefings.Reject)]
    public async Task<IActionResult> Reject(Guid id, RejectBriefingRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RejectBriefingCommand(id, request.Justification), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao rejeitar briefing");
    }

    /// <summary>
    /// Returns a briefing to its requester for corrections (BRF-019, RF014). Unlike a rejection, the
    /// briefing stays editable and can be resubmitted through <c>POST /briefings/{id}/submit</c>.
    /// </summary>
    [HttpPost("{id:guid}/request-changes")]
    [HasPermission(Permissions.Briefings.RequestAdjustments)]
    public async Task<IActionResult> RequestChanges(Guid id, RequestChangesBriefingRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RequestChangesBriefingCommand(id, request.Justification), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao solicitar ajustes no briefing");
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Briefings.ViewOwn)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBriefingByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar briefing");
    }

    /// <summary>
    /// Reads a briefing's progress and the tasks behind it (BRF-039, RF023) — the tracking screen's single
    /// read. The percentage is the domain's (BRF-035), computed per request and never cached.
    /// </summary>
    /// <remarks>
    /// Gated on <b>either</b> briefing-view permission: the requester reaches it through
    /// <c>briefings.view-own</c> and design management through <c>briefings.view-all</c>, and the seeded
    /// Design Manager holds only the latter (USR-047). Holding one merely opens the door — whether the
    /// caller is involved in <b>this</b> briefing is data the handler decides, answering <c>403</c> when
    /// they are not. The literal suffix cannot collide with <c>GET /briefings/{id:guid}</c>.
    /// </remarks>
    [HttpGet("{id:guid}/tracking")]
    [HasPermission(Permissions.Briefings.ViewOwn, Permissions.Briefings.ViewAll)]
    public async Task<IActionResult> GetTracking(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBriefingTrackingQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar o acompanhamento do briefing");
    }

    /// <summary>
    /// Lists the briefings awaiting review for the Design Manager's approval panel (BRF-020). Gated on
    /// <c>briefings.view-all</c>, the Manager's read capability — the role holds no <c>briefings.view-own</c>,
    /// so the ownership-scoped <c>GET /briefings</c> is not a substitute. The literal route cannot collide
    /// with <c>GET /briefings/{id:guid}</c>, which is guid-constrained.
    /// </summary>
    [HttpGet("pending-approval")]
    [HasPermission(Permissions.Briefings.ViewAll)]
    public async Task<IActionResult> GetPendingApproval(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetBriefingsPendingApprovalQuery(page, pageSize);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar briefings pendentes de aprovação");
    }

    /// <summary>
    /// Lists the briefings currently past their due instant (RF030, BRF-045), paginated, longest late first
    /// — the demand half of the SLA dashboard (BRF-046). Each row carries the delivery date that was missed,
    /// the requester and the category.
    /// </summary>
    /// <remarks>
    /// Gated on <c>briefings.view-all</c> alone: the list crosses every requester's demands, which is exactly
    /// what <c>briefings.view-own</c> does not open. Whether a briefing is late is not decided here — the SLA
    /// monitor (BRF-043) marked it, and this route reports the mark. The literal route cannot collide with
    /// <c>GET /briefings/{id:guid}</c>, which is guid-constrained.
    /// </remarks>
    [HttpGet("overdue")]
    [HasPermission(Permissions.Briefings.ViewAll)]
    public async Task<IActionResult> GetOverdue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetOverdueBriefingsQuery(page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar briefings atrasados");
    }

    [HttpGet]
    [HasPermission(Permissions.Briefings.ViewOwn)]
    public async Task<IActionResult> GetMyBriefings(
        [FromQuery] BriefingStatus? status,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetBriefingsByRequesterQuery(page, pageSize, status, categoryId);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar briefings");
    }
}
