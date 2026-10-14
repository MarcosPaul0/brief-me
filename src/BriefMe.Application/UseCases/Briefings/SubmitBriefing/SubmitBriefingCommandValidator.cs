using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.SubmitBriefing;

/// <summary>
/// Presence validation for the command. Field completeness is a domain invariant enforced by
/// <c>Briefing.Submit()</c>, not here.
/// </summary>
public sealed class SubmitBriefingCommandValidator : AbstractValidator<SubmitBriefingCommand>
{
    public SubmitBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
