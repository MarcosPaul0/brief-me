using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;

/// <summary>
/// Presence validation for the change-request command (BRF-018). As in BRF-017, the justification rule is not
/// restated here: it is delegated to <see cref="Justification.Create"/>, keeping the VO as the single source
/// of truth. The status invariant remains a domain rule enforced by <c>Briefing.RequestChanges()</c>.
/// </summary>
public sealed class RequestChangesBriefingCommandValidator : AbstractValidator<RequestChangesBriefingCommand>
{
    public RequestChangesBriefingCommandValidator()
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
