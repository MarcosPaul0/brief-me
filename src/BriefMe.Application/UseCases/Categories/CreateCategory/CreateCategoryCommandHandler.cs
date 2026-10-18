using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Categories.CreateCategory;

public sealed class CreateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await categoryRepository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
            return Result.Conflict<CategoryDto>(CategoryErrors.DuplicateName);

        var category = Category.Create(request.Name, request.Description);

        await categoryRepository.AddAsync(category, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(CategoryDto.FromCategory(category));
    }
}
