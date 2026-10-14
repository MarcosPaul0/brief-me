using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Categories.CreateCategory;

public sealed record CreateCategoryCommand(string Name, string? Description = null) : ICommand<CategoryDto>;
