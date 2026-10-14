using BriefMe.Domain.Entities;

namespace BriefMe.Domain.Repositories;

public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>Every category, active or not, ordered by name — the admin's list.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The active categories ordered by name — what a requester can pick for a new briefing.</summary>
    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether a category with this id exists and is active. False for both a missing and an inactive one.</summary>
    Task<bool> ExistsActiveAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether another category already uses <paramref name="name"/>, compared case-insensitively after
    /// trimming. <paramref name="ignoreId"/> excludes the category being renamed.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? ignoreId = null, CancellationToken cancellationToken = default);

    /// <summary>The categories whose ids are listed, in no particular order; unknown ids are skipped.</summary>
    Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
