using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

public sealed record RoleListItemDto(
    Guid RoleId,
    string Name,
    string? Description,
    bool IsSystemRole)
{
    public static RoleListItemDto FromRole(Role role)
    {
        return new RoleListItemDto(role.Id, role.Name, role.Description, role.IsSystemRole);
    }
}
