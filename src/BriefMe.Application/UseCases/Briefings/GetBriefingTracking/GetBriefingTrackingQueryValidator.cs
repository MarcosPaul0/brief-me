using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingTracking;

/// <summary>Presence validation for the tracking read (BRF-039).</summary>
public sealed class GetBriefingTrackingQueryValidator : AbstractValidator<GetBriefingTrackingQuery>
{
    public GetBriefingTrackingQueryValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
