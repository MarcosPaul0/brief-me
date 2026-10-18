using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Categories.ChangeCategoryStatus;
using BriefMe.Application.UseCases.Categories.CreateCategory;
using BriefMe.Application.UseCases.Categories.GetActiveCategories;
using BriefMe.Application.UseCases.Categories.GetCategories;
using BriefMe.Application.UseCases.Categories.UpdateCategory;
using BriefMe.Domain.Enums;
using BriefMe.Web.Contracts.Categories;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// Categories: the requester reads the active ones for the briefing form; the admin manages all of them.
/// </summary>
[ApiController]
[Route("categories")]
[Authorize]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet("active")]
    [Authorize(Roles = nameof(UserRole.Requester))]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetActiveCategoriesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar categorias");
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCategoriesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar categorias");
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateCategoryCommand(request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? Created($"/categories/{result.Value.Id}", result.Value)
            : result.ToProblem(this, "Falha ao criar categoria");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateCategoryCommand(id, request.Name, request.Description), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar categoria");
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeCategoryStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeCategoryStatusCommand(id, request.IsActive), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao alterar status da categoria");
    }
}
