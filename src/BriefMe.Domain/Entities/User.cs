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

    /// <summary>Object-store key of the uploaded avatar; null when the user has none (the UI shows the initial).</summary>
    public string? AvatarKey { get; private set; }

    /// <summary>Content type detected from the avatar's bytes (<see cref="AvatarFormat"/>), served back as-is.</summary>
    public string? AvatarContentType { get; private set; }

    /// <summary>
    /// Changes on every upload so clients can put it in the image URL and cache the response forever — a new
    /// avatar is a new URL. Null exactly when <see cref="AvatarKey"/> is null.
    /// </summary>
    public Guid? AvatarVersion { get; private set; }

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

    public void ChangeAvatar(string storageKey, AvatarFormat format)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("Chave do avatar é obrigatória.");

        ArgumentNullException.ThrowIfNull(format);

        AvatarKey = storageKey;
        AvatarContentType = format.ContentType;
        AvatarVersion = Guid.NewGuid();
    }

    public void RemoveAvatar()
    {
        AvatarKey = null;
        AvatarContentType = null;
        AvatarVersion = null;
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
