using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Categories.ChangeCategoryStatus;

public sealed class ChangeCategoryStatusCommandValidator : AbstractValidator<ChangeCategoryStatusCommand>
{
    public ChangeCategoryStatusCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(CategoryErrors.Validation.CategoryRequired);
    }
}
