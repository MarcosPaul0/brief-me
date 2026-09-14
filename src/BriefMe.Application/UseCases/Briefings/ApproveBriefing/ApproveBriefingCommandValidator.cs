using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.ApproveBriefing;

/// <summary>
/// Presence validation for the approval command (BRF-016). The status invariant is a domain rule enforced by
/// <c>Briefing.Approve()</c>; no justification applies to an approval (RF013 covers rejection only).
/// </summary>
public sealed class ApproveBriefingCommandValidator : AbstractValidator<ApproveBriefingCommand>
{
    public ApproveBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
