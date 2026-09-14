using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Categories;

namespace BriefMe.Application.UseCases.Categories.GetActiveCategories;

/// <summary>
/// Returns the active briefing categories for the create form's picker (RF040, BRF-013). No parameters:
/// the active-category catalog is the same for every requester in this slice (per-user scoping is not a
/// category concern).
/// </summary>
public sealed record GetActiveCategoriesQuery : IQuery<IReadOnlyList<CategoryListItemDto>>;
