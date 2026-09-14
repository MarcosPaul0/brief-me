using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Sla.ConfigureSlaRule;
using BriefMe.Domain.Constants;
using BriefMe.Web.Authorization;
using BriefMe.Web.Contracts.Sla;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// Exposes the SLA policy configuration over HTTP (BRF-042, RF027). A thin controller: request → command,
/// dispatch via <see cref="ISender"/>, map <c>Result&lt;T&gt;</c> to the response.
/// </summary>
/// <remarks>
/// Administrator-only by permission, not by role check: <c>sla.configure</c> is seeded to the Administrador
/// profile alone (USR-047), so a Design Manager calling this route gets a <c>403</c> without the handler ever
/// running.
/// </remarks>
[ApiController]
[Route("sla-rules")]
[Authorize]
public sealed class SlaRulesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Configures the deadline of one scope (RF027). Idempotent: the scope — category and/or priority — is
    /// the rule's identity, so sending the same body twice leaves the same single rule in place, which is
    /// why this is a <c>PUT</c> on the collection and not a <c>POST</c> that would promise a new row per
    /// call.
    /// </summary>
    [HttpPut]
    [HasPermission(Permissions.Configuration.SlaConfigure)]
    public async Task<IActionResult> Configure(ConfigureSlaRuleRequest request, CancellationToken cancellationToken)
    {
        var command = new ConfigureSlaRuleCommand(request.CategoryId, request.Priority, request.DeadlineHours);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao configurar regra de SLA");
    }
}
