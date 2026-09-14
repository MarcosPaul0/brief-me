namespace BriefMe.Domain.Repositories;

/// <summary>
/// A briefing category as needed by read scenarios (BRF-013 picker). A lightweight read projection, not the
/// full <c>Category</c> aggregate — that arrives with BRF-059/BRF-060.
/// </summary>
public sealed record CategoryInfo(Guid Id, string Name);

/// <summary>
/// Access to briefing categories (RF040). Defined in <c>Domain</c> per the project's Repository pattern.
/// BRF-006 needs only the active-category existence check to validate the briefing's mandatory association;
/// BRF-013 adds <see cref="GetActiveAsync"/> to feed the create form's category picker. BRF-059/BRF-060
/// introduce the <c>Category</c> aggregate and expand this interface (inheriting <see cref="IRepository{T}"/>)
/// with full CRUD; BRF-062 starts distinguishing "not found" from "inactive".
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Indicates whether a category with the given id exists and is active (RF040). Returns <c>false</c>
    /// for both a non-existent and an inactive category.
    /// </summary>
    Task<bool> ExistsActiveAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active categories a requester can pick when opening a briefing (RF040, BRF-013), ordered
    /// by name. Inactive categories are excluded so they cannot back new briefings.
    /// </summary>
    Task<IReadOnlyList<CategoryInfo>> GetActiveAsync(CancellationToken cancellationToken = default);
}
