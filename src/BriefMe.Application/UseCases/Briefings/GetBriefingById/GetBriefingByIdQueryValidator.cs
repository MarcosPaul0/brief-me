using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingById;

/// <summary>Presence validation for the single-briefing query (BRF-013).</summary>
public sealed class GetBriefingByIdQueryValidator : AbstractValidator<GetBriefingByIdQuery>
{
    public GetBriefingByIdQueryValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
