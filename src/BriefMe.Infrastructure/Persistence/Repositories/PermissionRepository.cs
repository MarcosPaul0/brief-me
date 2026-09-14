using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class PermissionRepository(AppDbContext dbContext) : EfRepository<Permission>(dbContext), IPermissionRepository
{
    public async Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await DbContext.Permissions.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
        await DbContext.Permissions.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Permissions.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default) =>
        await DbContext.Permissions.Where(p => codes.Contains(p.Code)).ToListAsync(cancellationToken);
}
