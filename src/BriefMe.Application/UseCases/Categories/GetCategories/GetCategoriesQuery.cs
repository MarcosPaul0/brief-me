using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Categories.GetCategories;

/// <summary>Every category, active or not, for the admin's management screen.</summary>
public sealed record GetCategoriesQuery : IQuery<IReadOnlyList<CategoryDto>>;
