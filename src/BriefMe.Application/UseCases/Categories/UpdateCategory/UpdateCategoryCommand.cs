using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Categories.UpdateCategory;

public sealed record UpdateCategoryCommand(Guid CategoryId, string Name, string? Description = null) : ICommand<CategoryDto>;
