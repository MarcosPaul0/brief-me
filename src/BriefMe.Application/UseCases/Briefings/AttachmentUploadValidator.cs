using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings;

/// <summary>
/// Per-file size rules shared by create and edit (ATT-04). The format is not checked here: it needs the
/// bytes, and <see cref="AttachmentFormat"/> stays the single source of truth for it.
/// </summary>
public sealed class AttachmentUploadValidator : AbstractValidator<AttachmentUpload>
{
    public AttachmentUploadValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage(x => BriefingErrors.Validation.AttachmentEmpty(x.FileName))
            .LessThanOrEqualTo(AttachmentFormat.MaxSizeBytes)
            .WithMessage(x => BriefingErrors.Validation.AttachmentTooLarge(x.FileName, AttachmentFormat.MaxSizeBytes));
    }
}
