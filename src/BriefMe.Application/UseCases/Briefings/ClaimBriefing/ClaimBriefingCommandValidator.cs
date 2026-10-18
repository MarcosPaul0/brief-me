using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.ClaimBriefing;

public sealed class ClaimBriefingCommandValidator : AbstractValidator<ClaimBriefingCommand>
{
    public ClaimBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
