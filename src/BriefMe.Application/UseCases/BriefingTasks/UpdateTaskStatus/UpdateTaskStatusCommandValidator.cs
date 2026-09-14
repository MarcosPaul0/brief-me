using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;

/// <summary>
/// Validation for the status update command (BRF-029). Presence and range only: whether the requested status
/// is reachable <b>from this task, by this caller</b> needs the aggregate and belongs to the handler.
/// </summary>
public sealed class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);

        // The enum starts at 1, so an unfilled status (default = 0) fails here rather than reaching the
        // aggregate as an "unknown status" DomainException.
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(BriefingTaskErrors.Validation.InvalidStatus);
    }
}
