using BriefMe.Domain.Entities;

namespace BriefMe.Domain.Repositories;

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? ignoreId = null, CancellationToken cancellationToken = default);
}
