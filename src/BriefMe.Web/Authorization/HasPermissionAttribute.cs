using Microsoft.AspNetCore.Authorization;

namespace BriefMe.Web.Authorization;

/// <summary>
/// Requires the caller to hold a permission (USR-045). Passing several requires <b>any</b> of them — see
/// <see cref="PermissionRequirement"/> — which is how an endpoint two profiles reach through two different
/// codes is gated (BRF-034). Stacking two attributes still means "all".
/// </summary>
public sealed class HasPermissionAttribute(params string[] permissions)
    : AuthorizeAttribute(policy: string.Join(PermissionRequirement.PolicySeparator, permissions));
