using BriefMe.Domain.Constants;
using BriefMe.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace BriefMe.Web.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Any of the listed codes satisfies the requirement (see PermissionRequirement); the administrator
        // holds the whole catalog by role and never needs the claim.
        if (context.User.IsInRole(Roles.Administrador) ||
            requirement.Permissions.Any(permission =>
                context.User.HasClaim(CustomClaimTypes.Permission, permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
