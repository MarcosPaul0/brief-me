using BriefMe.Domain.Common;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

public class Role : AggregateRoot, IAuditableEntity, ISoftDelete
{
    private readonly List<RolePermission> _rolePermissions = [];

    private Role(Guid id, string name, string? description, bool isSystemRole) : base(id)
    {
        Name = name;
        Description = description;
        IsSystemRole = isSystemRole;
    }

    private Role()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public static Role Create(string name, string? description, bool isSystemRole = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da role é obrigatório.");

        return new Role(Guid.NewGuid(), name.Trim(), description, isSystemRole);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da role é obrigatório.");

        Name = name.Trim();
    }

    public void ChangeDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void AddPermission(Guid permissionId)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permissionId))
            return;

        _rolePermissions.Add(RolePermission.Create(Id, permissionId));
    }

    public void RemovePermission(Guid permissionId)
    {
        _rolePermissions.RemoveAll(rp => rp.PermissionId == permissionId);
    }

    public void MarkAsDeleted(string? deletedBy)
    {
        if (IsSystemRole)
            throw new DomainException("Roles de sistema não podem ser excluídas.");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
