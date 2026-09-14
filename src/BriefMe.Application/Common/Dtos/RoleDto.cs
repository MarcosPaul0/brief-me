using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

public sealed record RoleDto(
    Guid RoleId,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyCollection<string> PermissionCodes)
{
    public static RoleDto FromRoleAndPermissions(Role role, IReadOnlyCollection<Permission> permissions)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            [.. permissions.Select(p => p.Code)]);
    }
    
    public static RoleDto FromRole(Role role)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            []);
    }
}
