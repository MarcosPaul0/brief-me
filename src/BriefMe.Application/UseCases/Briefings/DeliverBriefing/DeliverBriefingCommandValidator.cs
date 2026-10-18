using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.DeliverBriefing;

public sealed class DeliverBriefingCommandValidator : AbstractValidator<DeliverBriefingCommand>
{
    public DeliverBriefingCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        RuleFor(x => x.DeliveryNote)
            .NotEmpty().WithMessage(BriefingErrors.Validation.DeliveryNoteRequired)
            .Must(note => note is null || note.Trim().Length <= Briefing.DeliveryNoteMaxLength)
            .WithMessage(BriefingErrors.Validation.DeliveryNoteTooLong(Briefing.DeliveryNoteMaxLength));
    }
}
