using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Roles.CreateRole;
using BriefMe.Application.UseCases.Roles.GetRoleById;
using BriefMe.Application.UseCases.Roles.GetRoles;
using BriefMe.Application.UseCases.Roles.UpdateRole;
using BriefMe.Application.UseCases.Roles.UpdateRolePermissions;
using BriefMe.Web.Authorization;
using BriefMe.Web.Contracts.Roles;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

[ApiController]
[Route("roles")]
[Authorize]
public sealed class RolesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(request.Name, request.Description);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.RoleId }, result.Value)
            : result.ToProblem(this, "Falha ao criar role");
    }

    [HttpPut("{id:guid}")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateRoleCommand(id, request.Name, request.Description);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar role");
    }

    [HttpPut("{id:guid}/permissions")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateRolePermissionsCommand(id, request.PermissionIds);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar permissions da role");
    }

    [HttpGet]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRolesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar roles");
    }

    [HttpGet("{id:guid}")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetRoleByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao consultar role");
    }
}
