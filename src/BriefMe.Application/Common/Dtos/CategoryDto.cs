using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

/// <summary>A category as the admin manages it — including inactive ones, which requesters never see.</summary>
public sealed record CategoryDto(Guid Id, string Name, string? Description, bool IsActive)
{
    public static CategoryDto FromCategory(Category category) =>
        new(category.Id, category.Name, category.Description, category.IsActive);
}
