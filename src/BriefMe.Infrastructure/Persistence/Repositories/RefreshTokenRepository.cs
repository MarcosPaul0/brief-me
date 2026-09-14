using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AppDbContext dbContext) : EfRepository<RefreshToken>(dbContext), IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await DbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await ActiveByUserQuery(userId).ToListAsync(cancellationToken);

    public async Task RevokeAllActiveForUserAsync(Guid userId, string? revokedByIp, CancellationToken cancellationToken = default)
    {
        var activeTokens = await ActiveByUserQuery(userId).ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.Revoke(revokedByIp);
    }

    private IQueryable<RefreshToken> ActiveByUserQuery(Guid userId)
    {
        var now = DateTime.UtcNow;

        return DbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > now);
    }
}
