using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class CategoryRepository(AppDbContext dbContext) : EfRepository<Category>(dbContext), ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Categories
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsActiveAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await DbContext.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive, cancellationToken);

    public async Task<bool> NameExistsAsync(string name, Guid? ignoreId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        var query = DbContext.Categories.Where(c => c.Name.ToLower() == normalized);

        if (ignoreId is not null)
            query = query.Where(c => c.Id != ignoreId);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await DbContext.Categories
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);
    }
}
