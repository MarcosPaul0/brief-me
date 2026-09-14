using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BriefMe.Web.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var explicitPolicy = await base.GetPolicyAsync(policyName);
        if (explicitPolicy is not null)
        {
            return explicitPolicy;
        }

        // A policy name is one permission code, or several separated by PermissionRequirement's separator
        // when more than one profile legitimately reaches the same endpoint (BRF-034).
        return new AuthorizationPolicyBuilder()
            .AddRequirements(PermissionRequirement.FromPolicyName(policyName))
            .Build();
    }
}
