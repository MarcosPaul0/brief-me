using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.Common.Briefings;

/// <summary>
/// Loads the category and user names for a set of briefings in two queries, whatever the number of rows.
/// Shared by every briefing read so the lists and the detail screen name things the same way.
/// </summary>
public sealed class BriefingComplementsLoader(ICategoryRepository categoryRepository, IUserRepository userRepository)
{
    public async Task<BriefingComplements> LoadAsync(
        IReadOnlyCollection<Briefing> briefings,
        CancellationToken cancellationToken)
    {
        var categoryIds = briefings.Select(b => b.CategoryId).Distinct().ToList();
        var userIds = briefings
            .SelectMany(b => b.DesignerId is { } designerId ? [b.RequesterId, designerId] : new[] { b.RequesterId })
            .Distinct()
            .ToList();

        var categories = await categoryRepository.GetByIdsAsync(categoryIds, cancellationToken);
        var users = await userRepository.GetByIdsAsync(userIds, cancellationToken);

        return new BriefingComplements(
            categories.ToDictionary(c => c.Id, c => c.Name),
            users.ToDictionary(u => u.Id, u => u.FullName));
    }
}
