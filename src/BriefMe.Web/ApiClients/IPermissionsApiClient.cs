using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Permissions;
using BriefMe.Domain.Common;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Thin typed client for the read-only permission catalog (<c>GET /permissions</c>, USR-036), shared by
/// the catalog screen (USR-045) and the role editor (USR-044). Runs over the <c>"Api"</c> HttpClient, so
/// bearer-token attachment and refresh-on-401 come for free (USR-039).
/// </summary>
public interface IPermissionsApiClient
{
    /// <summary>
    /// Fetches the catalog grouped by module. A 403 surfaces as <see cref="Result.Forbidden{T}"/> so a
    /// screen can name the missing <c>permissions.manage</c> instead of rendering an empty editor.
    /// </summary>
    Task<Result<IReadOnlyList<PermissionGroupDto>>> GetPermissionGroupsAsync(CancellationToken cancellationToken = default);
}
