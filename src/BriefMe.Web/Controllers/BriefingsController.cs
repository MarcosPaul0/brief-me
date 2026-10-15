using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Application.UseCases.Briefings.ClaimBriefing;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Application.UseCases.Briefings.DeliverBriefing;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Application.UseCases.Briefings.GetBriefingQueue;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByDesigner;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;
using BriefMe.Application.UseCases.Briefings.SubmitBriefing;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// The briefing flow over HTTP. Requester actions (write, submit, cancel, accept, send back) and designer
/// actions (queue, claim, deliver) are gated per action by role; whether the caller owns or is assigned to
/// <b>this</b> briefing is the handler's answer (403).
/// </summary>
[ApiController]
[Route("briefings")]
[Authorize]
public sealed class BriefingsController(ISender sender) : ControllerBase
{
    private const string Requester = nameof(UserRole.Requester);
    private const string Designer = nameof(UserRole.Designer);

    // Room for a request carrying the maximum number of maximum-size attachments plus the form fields. Anything
    // bigger is cut off by Kestrel before it is buffered; below it, the validators answer per file (ATT-04).
    private const long AttachmentRequestSizeLimit = Briefing.MaxAttachments * AttachmentFormat.MaxSizeBytes + 1024 * 1024;

    /// <summary>
    /// <c>multipart/form-data</c>: the <see cref="CreateBriefingRequest"/> fields plus zero or more
    /// <c>attachments</c> file parts (ATT-01).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Requester)]
    [RequestSizeLimit(AttachmentRequestSizeLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentRequestSizeLimit)]
    public async Task<IActionResult> Create(
        [FromForm] CreateBriefingRequest request,
        [FromForm] List<IFormFile>? attachments,
        CancellationToken cancellationToken)
    {
        await using var uploads = new FormFileUploads(attachments);

        var command = new CreateBriefingCommand(
            request.Title,
            request.Priority,
            request.CategoryId,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate,
            uploads.Items);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Created($"/briefings/{result.Value.Id}", result.Value)
            : result.ToProblem(this, "Falha ao criar briefing");
    }

    /// <summary>
    /// <c>multipart/form-data</c>: the <see cref="UpdateBriefingRequest"/> fields, new <c>attachments</c> file parts
    /// and one <c>removedAttachmentIds</c> part per attachment to drop (ATT-08).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Requester)]
    [RequestSizeLimit(AttachmentRequestSizeLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentRequestSizeLimit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] UpdateBriefingRequest request,
        [FromForm] List<IFormFile>? attachments,
        [FromForm] List<Guid>? removedAttachmentIds,
        CancellationToken cancellationToken)
    {
        await using var uploads = new FormFileUploads(attachments);

        var command = new EditBriefingCommand(
            id,
            request.Title,
            request.Priority,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate,
            uploads.Items,
            removedAttachmentIds ?? []);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao editar briefing");
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = Requester)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitBriefingCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao submeter briefing para aprovação");
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Requester)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelBriefingCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao cancelar briefing");
    }

    /// <summary>Open to the owner, the assigned designer, and any designer while the briefing is queued.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Requester + "," + Designer)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBriefingByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar briefing");
    }

    [HttpGet]
    [Authorize(Roles = Requester)]
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

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = Requester)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AcceptBriefingDeliveryCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao aceitar a entrega");
    }

    [HttpPost("{id:guid}/request-changes")]
    [Authorize(Roles = Requester)]
    public async Task<IActionResult> RequestChanges(Guid id, RequestBriefingChangesRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RequestBriefingChangesCommand(id, request.Reason), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao solicitar ajustes");
    }

    /// <summary>The open queue: submitted briefings no designer has claimed yet, oldest first.</summary>
    [HttpGet("queue")]
    [Authorize(Roles = Designer)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetBriefingQueueQuery(page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar a fila de briefings");
    }

    /// <summary>The briefings the calling designer claimed.</summary>
    [HttpGet("assigned")]
    [Authorize(Roles = Designer)]
    public async Task<IActionResult> GetAssigned(
        [FromQuery] BriefingStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetBriefingsByDesignerQuery(page, pageSize, status), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar seus briefings");
    }

    [HttpPost("{id:guid}/claim")]
    [Authorize(Roles = Designer)]
    public async Task<IActionResult> Claim(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ClaimBriefingCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao assumir o briefing");
    }

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = Designer)]
    public async Task<IActionResult> Deliver(Guid id, DeliverBriefingRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeliverBriefingCommand(id, request.DeliveryNote), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao entregar o briefing");
    }

    /// <summary>
    /// Opens every posted file as an <see cref="AttachmentUpload"/> and closes them all once the command ran.
    /// Form-file streams sit on the buffered request body, so they are seekable as the stager requires.
    /// </summary>
    private sealed class FormFileUploads : IAsyncDisposable
    {
        public FormFileUploads(IReadOnlyList<IFormFile>? files)
        {
            Items = (files ?? []).Select(file => new AttachmentUpload(file.FileName, file.OpenReadStream(), file.Length)).ToList();
        }

        public IReadOnlyList<AttachmentUpload> Items { get; }

        public async ValueTask DisposeAsync()
        {
            foreach (var item in Items)
                await item.Content.DisposeAsync();
        }
    }
}
