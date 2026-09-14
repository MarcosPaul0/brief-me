using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.RejectBriefing;

/// <summary>
/// Presence validation for the rejection command (BRF-017). The justification rule (RF013) is not restated
/// here: it is delegated to <see cref="Justification.Create"/>, so the VO stays the single source of truth
/// for what a valid reason is. The status invariant remains a domain rule enforced by <c>Briefing.Reject()</c>.
/// </summary>
public sealed class RejectBriefingCommandValidator : AbstractValidator<RejectBriefingCommand>
{
    public RejectBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        RuleFor(x => x.Justification).Custom((justification, context) =>
        {
            var result = Justification.Create(justification);
            if (result.IsFailure)
                context.AddFailure(result.Error!);
        });
    }
}
