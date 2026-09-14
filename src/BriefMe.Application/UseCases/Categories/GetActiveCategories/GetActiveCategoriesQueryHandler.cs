using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Categories.GetActiveCategories;

/// <summary>
/// Reads the active briefing categories (RF040, BRF-013) via <see cref="ICategoryRepository"/> and maps them
/// to <see cref="CategoryListItemDto"/>. A pure read: no authorization beyond the endpoint policy, no
/// mutation. Backed today by a seeded catalog (BRF-059 replaces it with the EF-backed aggregate).
/// </summary>
public sealed class GetActiveCategoriesQueryHandler(ICategoryRepository categoryRepository)
    : IQueryHandler<GetActiveCategoriesQuery, IReadOnlyList<CategoryListItemDto>>
{
    public async Task<Result<IReadOnlyList<CategoryListItemDto>>> Handle(GetActiveCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetActiveAsync(cancellationToken);

        IReadOnlyList<CategoryListItemDto> items = categories
            .Select(category => new CategoryListItemDto(category.Id, category.Name))
            .ToList();

        return Result.Success(items);
    }
}
