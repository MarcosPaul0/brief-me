using BriefMe.Domain.Common;

namespace BriefMe.Domain.Repositories;

public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);

    IQueryable<T> Query();
}
