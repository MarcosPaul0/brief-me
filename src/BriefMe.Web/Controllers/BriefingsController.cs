using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;
using BriefMe.Domain.Enums;
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
[Authorize(Roles = nameof(UserRole.Requester))]
public sealed class BriefingsController(ISender sender) : ControllerBase
{
    [HttpPost]
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
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitBriefingForApprovalCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao submeter briefing para aprovação");
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelBriefingCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao cancelar briefing");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBriefingByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar briefing");
    }

    [HttpGet]
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
