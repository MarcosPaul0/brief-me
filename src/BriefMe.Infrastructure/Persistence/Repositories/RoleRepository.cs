using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class RoleRepository(AppDbContext dbContext) : EfRepository<Role>(dbContext), IRoleRepository
{
    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await DbContext.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public async Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
        await DbContext.Roles.Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Roles.ToListAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(string name, Guid? ignoreId = null, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Roles.Where(r => r.Name == name);

        if (ignoreId is not null)
            query = query.Where(r => r.Id != ignoreId);

        return await query.AnyAsync(cancellationToken);
    }
}
