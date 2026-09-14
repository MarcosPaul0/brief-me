using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Entities;

public class User : AggregateRoot, IAuditableEntity, ISoftDelete
{
    private readonly List<UserRole> _userRoles = [];

    private User(Guid id, string fullName, Email email, string passwordHash) : base(id)
    {
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
    }

    private User()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    public Email Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public static User Create(string fullName, Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Nome do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Hash de senha é obrigatório.");

        return new User(Guid.NewGuid(), fullName.Trim(), email, passwordHash);
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("Hash de senha é obrigatório.");

        PasswordHash = newPasswordHash;
    }

    public void UpdateProfile(string fullName, Email email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Nome do usuário é obrigatório.");

        FullName = fullName.Trim();
        Email = email;
    }

    public void Activate() => Status = UserStatus.Active;

    public void Deactivate() => Status = UserStatus.Inactive;

    public void AssignRole(Guid roleId)
    {
        if (_userRoles.Any(ur => ur.RoleId == roleId))
            return;

        _userRoles.Add(UserRole.Create(Id, roleId));
    }

    public void RemoveRole(Guid roleId)
    {
        _userRoles.RemoveAll(ur => ur.RoleId == roleId);
    }

    public void MarkAsDeleted(string? deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
