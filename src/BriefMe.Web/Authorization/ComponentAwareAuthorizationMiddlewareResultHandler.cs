using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Components.Endpoints;

namespace BriefMe.Web.Authorization;

/// <summary>
/// Razor component pages carry <c>[Authorize]</c> metadata for Blazor's own client-side
/// enforcement (<see cref="Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView"/>),
/// not for the HTTP pipeline: a normal page request never carries a bearer token (the JWT lives
/// in browser storage, restored inside the circuit — see USR-038). <see cref="ComponentTypeMetadata"/>
/// is present on every endpoint produced by <c>MapRazorComponents</c> (confirmed by inspecting
/// endpoint metadata at runtime) and only those — API controller and minimal-API endpoints never
/// carry it, so they keep the default challenge/forbid short-circuit.
/// </summary>
public sealed class ComponentAwareAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var isComponentEndpoint = context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;

        if (!authorizeResult.Succeeded && isComponentEndpoint)
            return next(context);

        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
