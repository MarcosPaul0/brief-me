using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;

public sealed class RequestBriefingChangesCommandValidator : AbstractValidator<RequestBriefingChangesCommand>
{
    public RequestBriefingChangesCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        // The value object is the single source of truth for what a valid reason is.
        RuleFor(x => x.Reason).Custom((reason, context) =>
        {
            var result = Justification.Create(reason);
            if (result.IsFailure)
                context.AddFailure(result.Error!);
        });
    }
}
