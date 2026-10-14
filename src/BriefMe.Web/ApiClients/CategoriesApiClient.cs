using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Categories;
using BriefMe.Web.Http;

namespace BriefMe.Web.ApiClients;

public sealed class CategoriesApiClient(AuthenticatedApiClient api) : ICategoriesApiClient
{
    private const string LoadFailureMessage = "Não foi possível carregar as categorias. Tente novamente.";
    private const string SaveFailureMessage = "Não foi possível salvar a categoria. Tente novamente.";
    private const string StatusFailureMessage = "Não foi possível alterar o status da categoria. Tente novamente.";

    public Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        api.GetResultAsync<IReadOnlyList<CategoryDto>, List<CategoryDto>>("categories", LoadFailureMessage, cancellationToken);

    public Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) =>
        api.SendForResultAsync<CategoryDto>(
            client => client.PostAsJsonAsync("categories", request, ApiProblemMapper.JsonOptions, cancellationToken),
            SaveFailureMessage,
            cancellationToken);

    public Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default) =>
        api.SendForResultAsync<CategoryDto>(
            client => client.PutAsJsonAsync($"categories/{id}", request, ApiProblemMapper.JsonOptions, cancellationToken),
            SaveFailureMessage,
            cancellationToken);

    public Task<Result<CategoryDto>> ChangeCategoryStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        api.SendForResultAsync<CategoryDto>(
            client => client.PatchAsJsonAsync($"categories/{id}/status", new ChangeCategoryStatusRequest(isActive), ApiProblemMapper.JsonOptions, cancellationToken),
            StatusFailureMessage,
            cancellationToken);
}
