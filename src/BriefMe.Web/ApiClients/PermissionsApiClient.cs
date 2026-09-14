using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Permissions;
using BriefMe.Domain.Common;
using BriefMe.Web.Http;

namespace BriefMe.Web.ApiClients;

public sealed class PermissionsApiClient : IPermissionsApiClient
{
    private const string LoadPermissionsFailureMessage = "Não foi possível carregar o catálogo de permissões. Tente novamente.";

    private readonly AuthenticatedApiClient _api;

    public PermissionsApiClient(AuthenticatedApiClient api)
    {
        _api = api;
    }

    public Task<Result<IReadOnlyList<PermissionGroupDto>>> GetPermissionGroupsAsync(CancellationToken cancellationToken = default) =>
        _api.GetResultAsync<IReadOnlyList<PermissionGroupDto>, List<PermissionGroupDto>>(
            "permissions", LoadPermissionsFailureMessage, cancellationToken);
}
