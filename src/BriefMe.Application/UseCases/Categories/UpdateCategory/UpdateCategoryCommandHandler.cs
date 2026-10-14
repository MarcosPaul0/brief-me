using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Categories.UpdateCategory;

public sealed class UpdateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateCategoryCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result.NotFound<CategoryDto>(CategoryErrors.NotFound);

        if (await categoryRepository.NameExistsAsync(request.Name, request.CategoryId, cancellationToken))
            return Result.Conflict<CategoryDto>(CategoryErrors.DuplicateName);

        category.Rename(request.Name);
        category.ChangeDescription(request.Description);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(CategoryDto.FromCategory(category));
    }
}
