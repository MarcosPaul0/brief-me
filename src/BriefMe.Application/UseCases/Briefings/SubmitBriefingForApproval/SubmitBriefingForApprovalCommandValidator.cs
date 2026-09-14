using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;

/// <summary>
/// Presence validation for the submission command (BRF-007). Field-completeness for approval (RF005) is a
/// domain invariant enforced by <c>Briefing.SubmitForApproval()</c>, not here.
/// </summary>
public sealed class SubmitBriefingForApprovalCommandValidator : AbstractValidator<SubmitBriefingForApprovalCommand>
{
    public SubmitBriefingForApprovalCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);
    }
}
