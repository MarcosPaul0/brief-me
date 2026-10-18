using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.Categories.UpdateCategory;

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(CategoryErrors.Validation.CategoryRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(CategoryErrors.Validation.NameRequired)
            .MaximumLength(Category.NameMaxLength).WithMessage(CategoryErrors.Validation.NameTooLong(Category.NameMaxLength));

        RuleFor(x => x.Description)
            .MaximumLength(Category.DescriptionMaxLength)
            .WithMessage(CategoryErrors.Validation.DescriptionTooLong(Category.DescriptionMaxLength));
    }
}
