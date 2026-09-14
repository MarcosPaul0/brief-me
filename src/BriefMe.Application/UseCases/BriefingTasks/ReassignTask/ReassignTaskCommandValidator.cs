using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.ReassignTask;

/// <summary>
/// Presence validation for the reassignment command (BRF-038). Whether the target is a Designer, and whether
/// the task can still change hands, are business rules the handler and <c>BriefingTask.ReassignTo()</c>
/// decide — they need the database. Here we only keep an empty id from reaching the aggregate as a
/// <c>DomainException</c> (500) instead of a validation failure (400).
/// </summary>
public sealed class ReassignTaskCommandValidator : AbstractValidator<ReassignTaskCommand>
{
    public ReassignTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);

        RuleFor(x => x.DesignerId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.DesignerRequired);
    }
}
