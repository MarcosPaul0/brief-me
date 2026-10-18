using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Categories;

namespace BriefMe.Web.ApiClients;

/// <summary>Typed client for the admin's category screen.</summary>
public interface ICategoriesApiClient
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<Result<CategoryDto>> ChangeCategoryStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
