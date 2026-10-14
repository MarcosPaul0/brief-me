using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.CreateBriefing;

/// <summary>
/// Presence validation of the fields needed to save a draft. Completeness is only required on submission
/// (<c>Briefing.Submit()</c>), so description, objective, area and date are optional here.
/// </summary>
public sealed class CreateBriefingCommandValidator : AbstractValidator<CreateBriefingCommand>
{
    public CreateBriefingCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(BriefingErrors.Validation.TitleRequired);

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(CategoryErrors.Validation.CategoryRequired);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(BriefingErrors.Validation.InvalidPriority);
    }
}
