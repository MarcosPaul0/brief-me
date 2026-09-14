using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Roles.GetRoleById;

public sealed class GetRoleByIdQueryHandler(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository) : IQueryHandler<GetRoleByIdQuery, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetByIdWithPermissionsAsync(request.RoleId, cancellationToken);
        if (role is null)
            return Result.NotFound<RoleDto>(RoleErrors.NotFound);

        var permissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();
        var permissions = permissionIds.Count == 0
            ? []
            : await permissionRepository.GetByIdsAsync(permissionIds, cancellationToken);

        return Result.Success(RoleDto.FromRoleAndPermissions(role, permissions));
    }
}
