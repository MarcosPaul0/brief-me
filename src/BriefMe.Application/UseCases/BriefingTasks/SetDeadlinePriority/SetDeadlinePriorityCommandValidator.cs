using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;

/// <summary>
/// Validation for the planning command (BRF-028). Unlike the other task validators this one also carries a
/// business rule — a deadline in the past — because the BRF-028 acceptance criterion asks for a
/// <b>validation failure</b> there, and the decision needs nothing but the command itself.
/// </summary>
public sealed class SetDeadlinePriorityCommandValidator : AbstractValidator<SetDeadlinePriorityCommand>
{
    public SetDeadlinePriorityCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);

        // Today is read per validation, never cached in a field: the validator is resolved from the container
        // and may outlive the day it was built.
        // The past rule is skipped for an unfilled date so the caller reads "deadline is required" rather than
        // "deadline cannot be in the past" — default(DateOnly) is 0001-01-01 and would trip both rules.
        RuleFor(x => x.Deadline)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.DeadlineRequired);

        RuleFor(x => x.Deadline)
            .Must(deadline => deadline >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(BriefingTaskErrors.Validation.DeadlineInPast)
            .When(x => x.Deadline != default);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(BriefingTaskErrors.Validation.InvalidPriority);
    }
}
