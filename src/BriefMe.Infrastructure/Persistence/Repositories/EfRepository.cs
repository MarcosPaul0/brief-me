using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class EfRepository<T>(AppDbContext dbContext) : IRepository<T> where T : Entity
{
    protected AppDbContext DbContext { get; } = dbContext;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Set<T>().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbContext.Set<T>().AddAsync(entity, cancellationToken);

    public void Update(T entity) => DbContext.Set<T>().Update(entity);

    public void Remove(T entity) => DbContext.Set<T>().Remove(entity);

    public IQueryable<T> Query() => DbContext.Set<T>();
}
