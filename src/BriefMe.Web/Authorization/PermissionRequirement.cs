using Microsoft.AspNetCore.Authorization;

namespace BriefMe.Web.Authorization;

/// <summary>
/// One or more permission codes, satisfied by holding <b>any</b> of them. A single code is the common case
/// (<c>[HasPermission(Permissions.Tasks.Create)]</c>); several express a read that more than one profile
/// legitimately reaches — the task detail (BRF-034) is open to the assignee through
/// <c>tasks.view-assigned</c> and to design management through <c>tasks.view-all</c>, two permissions for
/// the same door.
/// </summary>
/// <remarks>
/// "Any" rather than "all" because stacking two <c>[Authorize]</c> attributes already means "all": a policy
/// requiring every listed permission needs no new mechanism, an alternative one had none. Which of the
/// codes the caller held never changes what the endpoint does — scoping the <i>data</i> stays the handler's
/// job.
/// </remarks>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Separator between codes inside a policy name. No code in <c>Domain.Constants.Permissions</c>
    /// contains it, so a single-permission policy name parses back to exactly itself.
    /// </summary>
    public const char PolicySeparator = ',';

    public PermissionRequirement(params string[] permissions) => Permissions = permissions;

    public IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Splits a policy name into the codes it accepts, dropping blanks so a stray separator cannot widen
    /// the policy into one an empty claim would satisfy.
    /// </summary>
    public static PermissionRequirement FromPolicyName(string policyName) =>
        new(policyName.Split(
            PolicySeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
