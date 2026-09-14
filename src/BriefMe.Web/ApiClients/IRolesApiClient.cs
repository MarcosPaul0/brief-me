using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Roles;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Roles;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Thin typed client the roles screen (USR-044) uses to reach the REST API — keeps HTTP concerns out of
/// the Razor components (CLAUDE.md: components stay thin). Runs over the <c>"Api"</c> HttpClient, so
/// bearer-token attachment and refresh-on-401 come for free (USR-039).
/// </summary>
public interface IRolesApiClient
{
    /// <summary>Lists the access profiles via <c>GET /roles</c> (requires <c>roles.manage</c>).</summary>
    Task<Result<IReadOnlyList<RoleListItemDto>>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single role with its permission codes via <c>GET /roles/{id}</c>.
    /// A 404 surfaces as <see cref="Result.NotFound{T}"/>.
    /// </summary>
    Task<Result<RoleDto>> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a role's permission set via <c>PUT /roles/{id}/permissions</c> and returns the persisted
    /// state.
    /// </summary>
    Task<Result<RoleDto>> UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
}
