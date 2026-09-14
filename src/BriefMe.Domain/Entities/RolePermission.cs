using BriefMe.Domain.Common;

namespace BriefMe.Domain.Entities;

public class RolePermission : IAuditableEntity
{
    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public Role? Role { get; private set; }

    public Permission? Permission { get; private set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    internal static RolePermission Create(Guid roleId, Guid permissionId) => new(roleId, permissionId);
}
