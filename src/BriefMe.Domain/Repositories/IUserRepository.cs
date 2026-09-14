using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(Email email, Guid? ignoreId = null, CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithRolesAndPermissionsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the users matching the given ids, in a single query. Feeds read models that must show a
    /// person's name next to another aggregate's foreign key (BRF-020: the requester of each briefing in the
    /// approval queue) without an N+1 lookup. Missing/soft-deleted ids are simply absent from the result.
    /// </summary>
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of users matching <paramref name="filter"/>, in <paramref name="sort"/> order. The
    /// total behind the filter comes from <see cref="CountAsync"/>.
    /// </summary>
    Task<IReadOnlyList<User>> GetPagedAsync(
        UserFilter filter,
        int page,
        int pageSize,
        UserSort sort,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the users matching <paramref name="filter"/>, ignoring pagination and sort order.
    /// </summary>
    Task<int> CountAsync(UserFilter filter, CancellationToken cancellationToken = default);
}
