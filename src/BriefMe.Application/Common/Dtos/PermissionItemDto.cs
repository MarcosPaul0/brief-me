using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

public sealed record PermissionItemDto(
    Guid PermissionId,
    string Code,
    string Name,
    string? Description)
{
    public static PermissionItemDto FromPermission(Permission permission)
    {
        return new(
            permission.Id,
            permission.Code,
            permission.Name,
            permission.Description);
    }
}
