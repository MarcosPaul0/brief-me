using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext dbContext) : EfRepository<User>(dbContext), IUserRepository
{
    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        await DbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<bool> EmailExistsAsync(Email email, Guid? ignoreId = null, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Users.Where(u => u.Email == email);

        if (ignoreId is not null)
            query = query.Where(u => u.Id != ignoreId);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByIdWithRolesAndPermissionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await DbContext.Users
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetPagedAsync(
        UserFilter filter,
        int page,
        int pageSize,
        UserSort sort,
        CancellationToken cancellationToken = default)
    {
        // Roles are eager-loaded for the page only; the count query has no use for them.
        var query = Filtered(DbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role), filter);

        return await ApplySort(query, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(UserFilter filter, CancellationToken cancellationToken = default) =>
        await Filtered(DbContext.Users, filter).CountAsync(cancellationToken);

    private static IQueryable<User> Filtered(IQueryable<User> query, UserFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(u => u.FullName.ToLower().Contains(search) || u.Email.Value.ToLower().Contains(search));
        }

        if (filter.Status is not null)
            query = query.Where(u => u.Status == filter.Status);

        if (filter.RoleId is not null)
            query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == filter.RoleId));

        return query;
    }

    private static IQueryable<User> ApplySort(IQueryable<User> query, UserSort sort) => sort.Field switch
    {
        UserSortField.Email => sort.Descending
            ? query.OrderByDescending(u => u.Email.Value)
            : query.OrderBy(u => u.Email.Value),
        UserSortField.CreatedAt => sort.Descending
            ? query.OrderByDescending(u => u.CreatedAt)
            : query.OrderBy(u => u.CreatedAt),
        _ => sort.Descending
            ? query.OrderByDescending(u => u.FullName)
            : query.OrderBy(u => u.FullName)
    };
}
