using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.Categories.CreateCategory;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(CategoryErrors.Validation.NameRequired)
            .MaximumLength(Category.NameMaxLength).WithMessage(CategoryErrors.Validation.NameTooLong(Category.NameMaxLength));

        RuleFor(x => x.Description)
            .MaximumLength(Category.DescriptionMaxLength)
            .WithMessage(CategoryErrors.Validation.DescriptionTooLong(Category.DescriptionMaxLength));
    }
}
