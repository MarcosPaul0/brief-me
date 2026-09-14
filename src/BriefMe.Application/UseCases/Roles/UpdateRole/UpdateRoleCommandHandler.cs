using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Roles.UpdateRole;

public sealed class UpdateRoleCommandHandler(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetByIdWithPermissionsAsync(request.RoleId, cancellationToken);
        if (role is null)
            return Result.NotFound<RoleDto>(RoleErrors.NotFound);

        var name = request.Name.Trim();

        if (await roleRepository.NameExistsAsync(name, request.RoleId, cancellationToken))
            return Result.Conflict<RoleDto>(RoleErrors.DuplicateName);

        role.Rename(name);
        role.ChangeDescription(request.Description);

        await unitOfWork.CommitAsync(cancellationToken);

        var permissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();
        var permissions = permissionIds.Count == 0
            ? []
            : await permissionRepository.GetByIdsAsync(permissionIds, cancellationToken);

        return Result.Success(RoleDto.FromRoleAndPermissions(role, permissions));
    }
}
