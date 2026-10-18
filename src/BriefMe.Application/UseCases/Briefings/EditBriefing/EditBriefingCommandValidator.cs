using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.EditBriefing;

/// <summary>
/// Presence validation for editing a briefing. Which states allow editing is a domain invariant enforced by
/// <c>Briefing.Edit()</c> and guarded in the handler, not here.
/// </summary>
public sealed class EditBriefingCommandValidator : AbstractValidator<EditBriefingCommand>
{
    public EditBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(BriefingErrors.Validation.TitleRequired);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(BriefingErrors.Validation.InvalidPriority);

        // The total after the edit (existing − removed + new) needs the briefing; the handler checks it.
        RuleFor(x => x.NewAttachments!.Count)
            .LessThanOrEqualTo(Briefing.MaxAttachments)
            .WithMessage(BriefingErrors.TooManyAttachments(Briefing.MaxAttachments))
            .OverridePropertyName(nameof(EditBriefingCommand.NewAttachments))
            .When(x => x.NewAttachments is not null);

        RuleForEach(x => x.NewAttachments)
            .SetValidator(new AttachmentUploadValidator());

        RuleForEach(x => x.RemovedAttachmentIds)
            .NotEmpty().WithMessage(BriefingErrors.Validation.AttachmentIdRequired);
    }
}
