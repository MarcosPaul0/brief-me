using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Categories.ChangeCategoryStatus;

public sealed class ChangeCategoryStatusCommandHandler(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<ChangeCategoryStatusCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(ChangeCategoryStatusCommand request, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result.NotFound<CategoryDto>(CategoryErrors.NotFound);

        if (request.IsActive)
            category.Activate();
        else
            category.Deactivate();

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(CategoryDto.FromCategory(category));
    }
}
