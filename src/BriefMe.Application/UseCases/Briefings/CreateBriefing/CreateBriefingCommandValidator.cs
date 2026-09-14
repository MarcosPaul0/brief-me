using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.CreateBriefing;

/// <summary>
/// Presence validation of the fields needed to save the draft (BRF-006). Validates presence only, not
/// completeness for approval — RF005 completeness is required on submission (BRF-007), and the category
/// checklist in BRF-093. Therefore Description/Objective/RequestingArea/DesiredDeliveryDate are optional in
/// the draft.
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
