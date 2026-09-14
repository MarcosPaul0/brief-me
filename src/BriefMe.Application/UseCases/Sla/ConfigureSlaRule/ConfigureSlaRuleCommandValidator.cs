using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Sla.ConfigureSlaRule;

/// <summary>
/// Validates the configuration command before it reaches the aggregate (BRF-042). The deadline range is not
/// restated here: it is delegated to <see cref="SlaDeadline.Create"/>, which stays the single source of what
/// a valid deadline is. Neither scope side is required — a missing side is a wildcard, not a blank field.
/// </summary>
public sealed class ConfigureSlaRuleCommandValidator : AbstractValidator<ConfigureSlaRuleCommand>
{
    public ConfigureSlaRuleCommandValidator()
    {
        RuleFor(x => x.DeadlineHours).Custom((deadlineHours, context) =>
        {
            var result = SlaDeadline.Create(deadlineHours);
            if (result.IsFailure)
                context.AddFailure(result.Error!);
        });

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(SlaErrors.Validation.InvalidPriority)
            .When(x => x.Priority is not null);
    }
}
