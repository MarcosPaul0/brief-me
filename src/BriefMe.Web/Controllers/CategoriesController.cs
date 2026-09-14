using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Categories.GetActiveCategories;
using BriefMe.Domain.Constants;
using BriefMe.Web.Authorization;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// Exposes the active briefing categories that back the create form's picker (BRF-013, RF040). Read-only:
/// full category CRUD arrives with BRF-059/BRF-061. Gated on <c>briefings.create</c> since the picker only
/// serves users opening a briefing.
/// </summary>
[ApiController]
[Route("categories")]
[Authorize]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Briefings.Create)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetActiveCategoriesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar categorias");
    }
}
