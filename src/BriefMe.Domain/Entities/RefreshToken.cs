using BriefMe.Domain.Common;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

public class RefreshToken : Entity
{
    private RefreshToken(Guid id, string tokenHash, Guid userId, DateTime expiresAt, string? createdByIp)
        : base(id)
    {
        TokenHash = tokenHash;
        UserId = userId;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    private RefreshToken()
    {
    }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? RevokedByIp { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive => !IsExpired && !IsRevoked;

    public static RefreshToken Create(string tokenHash, Guid userId, DateTime expiresAt, string? createdByIp) =>
        new(Guid.NewGuid(), tokenHash, userId, expiresAt, createdByIp);

    public void Revoke(string? revokedByIp, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
            throw new DomainException("Refresh token já foi revogado.");

        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
