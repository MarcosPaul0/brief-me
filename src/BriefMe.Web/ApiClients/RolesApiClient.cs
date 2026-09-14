using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Roles;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Roles;
using BriefMe.Web.Http;

namespace BriefMe.Web.ApiClients;

public sealed class RolesApiClient : IRolesApiClient
{
    private const string LoadRolesFailureMessage = "Não foi possível carregar os perfis. Tente novamente.";
    private const string LoadRoleFailureMessage = "Não foi possível carregar o perfil. Tente novamente.";
    private const string SavePermissionsFailureMessage = "Não foi possível salvar as permissões do perfil. Tente novamente.";

    private readonly AuthenticatedApiClient _api;

    public RolesApiClient(AuthenticatedApiClient api)
    {
        _api = api;
    }

    public Task<Result<IReadOnlyList<RoleListItemDto>>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        _api.GetResultAsync<IReadOnlyList<RoleListItemDto>, List<RoleListItemDto>>(
            "roles", LoadRolesFailureMessage, cancellationToken);

    public Task<Result<RoleDto>> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        _api.GetResultAsync<RoleDto, RoleDto>(
            $"roles/{roleId}", LoadRoleFailureMessage, cancellationToken);

    public async Task<Result<RoleDto>> UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.PutAsJsonAsync($"roles/{roleId}/permissions", request, ApiProblemMapper.JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<RoleDto>(response, SavePermissionsFailureMessage, cancellationToken: cancellationToken);

            var role = await response.Content.ReadFromJsonAsync<RoleDto>(ApiProblemMapper.JsonOptions, cancellationToken);

            return role is null
                ? Result.Failure<RoleDto>(SavePermissionsFailureMessage)
                : Result.Success(role);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<RoleDto>(SavePermissionsFailureMessage);
        }
    }
}
