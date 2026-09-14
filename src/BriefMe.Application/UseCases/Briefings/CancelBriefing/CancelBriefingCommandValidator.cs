using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.CancelBriefing;

public sealed class CancelBriefingCommandValidator : AbstractValidator<CancelBriefingCommand>
{
    public CancelBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}