using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;

/// <summary>
/// Presence and shape validation for the delivery command (BRF-031). Deliberately does <b>not</b> check the
/// maximum file size or the allowed extensions: those are configurable rules owned by <c>IFileValidator</c>
/// (BRF-011), which reads them from Infrastructure settings the Application layer must not know about. What
/// is checked here is what no configuration can change — a delivery needs a task, a name and bytes.
/// </summary>
public sealed class SubmitTaskDeliveryCommandValidator : AbstractValidator<SubmitTaskDeliveryCommand>
{
    public SubmitTaskDeliveryCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);

        // Length is measured on the raw name while Attachment.Create* measures it trimmed — the two only
        // disagree for a name already over the limit that also carries padding, where failing is safer.
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.FileNameRequired)
            .MaximumLength(Attachment.MaxFileNameLength)
            .WithMessage(BriefingTaskErrors.Validation.FileNameTooLong(Attachment.MaxFileNameLength));

        RuleFor(x => x.Content)
            .NotNull().WithMessage(BriefingTaskErrors.Validation.FileContentRequired);

        // A zero-byte upload is a failed transfer, not a delivery. The upper bound is IFileValidator's.
        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage(BriefingTaskErrors.Validation.FileSizeMustBePositive);
    }
}
