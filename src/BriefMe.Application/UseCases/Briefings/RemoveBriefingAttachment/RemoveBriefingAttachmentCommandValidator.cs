using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.RemoveBriefingAttachment;

/// <summary>Presence validation for the removal (BRF-013): both ids identify what to drop and from where.</summary>
public sealed class RemoveBriefingAttachmentCommandValidator : AbstractValidator<RemoveBriefingAttachmentCommand>
{
    public RemoveBriefingAttachmentCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        RuleFor(x => x.AttachmentId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.AttachmentRequired);
    }
}
