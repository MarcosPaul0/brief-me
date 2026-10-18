using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the default briefing categories so a fresh system can take briefings before the admin configures
/// anything. Idempotent by name: only the missing ones are added, and nothing the admin changed is touched.
/// </summary>
public sealed class CategorySeeder(ICategoryRepository categories, IUnitOfWork unitOfWork) : IDataSeeder
{
    private static readonly string[] DefaultNames =
    [
        "Campanha de Marketing",
        "Material Institucional",
        "Redes Sociais",
        "Evento",
        "Identidade Visual",
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var changed = false;

        foreach (var name in DefaultNames)
        {
            if (await categories.NameExistsAsync(name, cancellationToken: cancellationToken))
                continue;

            await categories.AddAsync(Category.Create(name), cancellationToken);
            changed = true;
        }

        if (changed)
            await unitOfWork.CommitAsync(cancellationToken);
    }
}
