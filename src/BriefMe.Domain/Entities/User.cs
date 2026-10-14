using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Entities;

public class User : AggregateRoot, IAuditableEntity, ISoftDelete
{
    private User(Guid id, string fullName, Email email, string passwordHash, UserRole role) : base(id)
    {
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        Status = UserStatus.Active;
    }

    private User()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    public Email Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; }

    public UserRole Role { get; private set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public static User Create(string fullName, Email email, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Nome do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Hash de senha é obrigatório.");

        if (!Enum.IsDefined(role))
            throw new DomainException("Perfil do usuário inválido.");

        return new User(Guid.NewGuid(), fullName.Trim(), email, passwordHash, role);
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

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
            throw new DomainException("Perfil do usuário inválido.");

        Role = role;
    }

    public void MarkAsDeleted(string? deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
