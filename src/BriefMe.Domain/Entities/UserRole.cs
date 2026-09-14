using BriefMe.Domain.Common;

namespace BriefMe.Domain.Entities;

public class UserRole : IAuditableEntity
{
    private UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public User? User { get; private set; }

    public Role? Role { get; private set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    internal static UserRole Create(Guid userId, Guid roleId) => new(userId, roleId);
}
