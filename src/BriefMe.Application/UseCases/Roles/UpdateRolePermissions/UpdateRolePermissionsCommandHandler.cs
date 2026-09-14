using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Roles.UpdateRolePermissions;

public sealed class UpdateRolePermissionsCommandHandler(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateRolePermissionsCommand, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetByIdWithPermissionsAsync(request.RoleId, cancellationToken);
        if (role is null)
            return Result.NotFound<RoleDto>(RoleErrors.NotFound);

        var distinctPermissionIds = request.PermissionIds.Distinct().ToList();

        IReadOnlyList<Permission> permissions = distinctPermissionIds.Count == 0
            ? []
            : await permissionRepository.GetByIdsAsync(distinctPermissionIds, cancellationToken);

        if (permissions.Count != distinctPermissionIds.Count)
            return Result.Failure<RoleDto>(RoleErrors.InvalidPermissions);

        ReconcilePermissions(role, distinctPermissionIds);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(RoleDto.FromRoleAndPermissions(role, permissions));
    }

    private static void ReconcilePermissions(Role role, IReadOnlyCollection<Guid> targetPermissionIds)
    {
        var currentPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();

        foreach (var permissionId in currentPermissionIds.Except(targetPermissionIds))
            role.RemovePermission(permissionId);

        foreach (var permissionId in targetPermissionIds.Except(currentPermissionIds))
            role.AddPermission(permissionId);
    }
}
