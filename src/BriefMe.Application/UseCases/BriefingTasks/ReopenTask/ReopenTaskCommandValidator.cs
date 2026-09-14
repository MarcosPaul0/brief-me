using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.ReopenTask;

/// <summary>
/// Validation for the reopening command (BRF-037). Presence only — whether the task may be reopened depends
/// on its status and on its briefing's, which the handler reads from the database.
/// </summary>
public sealed class ReopenTaskCommandValidator : AbstractValidator<ReopenTaskCommand>
{
    public ReopenTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);
    }
}
