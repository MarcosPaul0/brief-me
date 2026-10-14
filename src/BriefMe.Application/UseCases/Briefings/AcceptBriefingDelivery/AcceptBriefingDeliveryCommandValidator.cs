using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;

public sealed class AcceptBriefingDeliveryCommandValidator : AbstractValidator<AcceptBriefingDeliveryCommand>
{
    public AcceptBriefingDeliveryCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
