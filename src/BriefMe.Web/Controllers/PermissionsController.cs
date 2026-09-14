using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Permissions.GetPermissions;
using BriefMe.Web.Authorization;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

[ApiController]
[Route("permissions")]
[Authorize]
public sealed class PermissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("permissions.manage")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPermissionsQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar permissions");
    }
}
