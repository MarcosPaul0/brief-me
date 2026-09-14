using BriefMe.Domain.Entities;

namespace BriefMe.Domain.Repositories;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RevokeAllActiveForUserAsync(Guid userId, string? revokedByIp, CancellationToken cancellationToken = default);
}
