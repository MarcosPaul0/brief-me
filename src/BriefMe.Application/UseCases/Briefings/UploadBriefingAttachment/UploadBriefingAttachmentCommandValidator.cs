using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;

/// <summary>
/// Presence and shape validation for the attachment upload (BRF-013). Deliberately does <b>not</b> check the
/// maximum file size or the allowed extensions: those are configurable rules owned by <c>IFileValidator</c>
/// (BRF-011), which reads them from Infrastructure settings the Application layer must not know about. What
/// is checked here is what no configuration can change — an attachment needs a briefing, a name and bytes.
/// </summary>
public sealed class UploadBriefingAttachmentCommandValidator : AbstractValidator<UploadBriefingAttachmentCommand>
{
    public UploadBriefingAttachmentCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        // Length is measured on the raw name while Attachment.Create measures it trimmed — the two only
        // disagree for a name already over the limit that also carries padding, where failing is safer.
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage(BriefingErrors.Validation.FileNameRequired)
            .MaximumLength(Attachment.MaxFileNameLength)
            .WithMessage(BriefingErrors.Validation.FileNameTooLong(Attachment.MaxFileNameLength));

        RuleFor(x => x.Content)
            .NotNull().WithMessage(BriefingErrors.Validation.FileContentRequired);

        // A zero-byte upload is a failed transfer, not an attachment. The upper bound is IFileValidator's.
        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage(BriefingErrors.Validation.FileSizeMustBePositive);
    }
}
