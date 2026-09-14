using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.AssignTask;

/// <summary>
/// Presence validation for the assignment command (BRF-027). Whether the target is a Designer, and whether
/// the task can still be assigned, are business rules the handler and <c>BriefingTask.AssignTo()</c> decide —
/// they need the database. Here we only keep an empty id from reaching the aggregate as a
/// <c>DomainException</c> (500) instead of a validation failure (400).
/// </summary>
public sealed class AssignTaskCommandValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);

        RuleFor(x => x.DesignerId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.DesignerRequired);
    }
}
