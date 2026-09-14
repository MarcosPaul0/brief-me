using BriefMe.Domain.Repositories;

namespace BriefMe.Infrastructure.Persistence.Repositories;

/// <summary>
/// Placeholder <see cref="ICategoryRepository"/> backed by a fixed in-memory catalog of active categories.
/// It exists so the Briefings vertical is runtime-wired before the Categories module lands: it satisfies
/// <see cref="ExistsActiveAsync"/> (RF040 validation on create, BRF-006) and <see cref="GetActiveAsync"/>
/// (the create form's picker, BRF-013). BRF-059/BRF-060 replace it with the EF-backed <c>Category</c>
/// aggregate + migration; the Domain interface and the Application query survive that swap unchanged.
/// Registered as a singleton — the catalog is immutable seed data with no per-request state.
/// </summary>
public sealed class SeededCategoryRepository : ICategoryRepository
{
    // Stable ids so a category selected in the form keeps validating across requests/restarts.
    private static readonly IReadOnlyList<CategoryInfo> Categories =
    [
        new(Guid.Parse("a1000000-0000-0000-0000-000000000001"), "Campanha de Marketing"),
        new(Guid.Parse("a1000000-0000-0000-0000-000000000002"), "Material Institucional"),
        new(Guid.Parse("a1000000-0000-0000-0000-000000000003"), "Redes Sociais"),
        new(Guid.Parse("a1000000-0000-0000-0000-000000000004"), "Evento"),
        new(Guid.Parse("a1000000-0000-0000-0000-000000000005"), "Identidade Visual"),
    ];

    private static readonly IReadOnlyList<CategoryInfo> OrderedCategories =
        Categories.OrderBy(category => category.Name, StringComparer.CurrentCulture).ToList();

    public Task<bool> ExistsActiveAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Categories.Any(category => category.Id == categoryId));

    public Task<IReadOnlyList<CategoryInfo>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OrderedCategories);
}
