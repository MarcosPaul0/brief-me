using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.GetTaskDetail;

/// <summary>
/// Shape guard for the task detail read (BRF-034). Whether the task exists is the handler's answer
/// (<c>NotFound</c>) and whether the caller may read it is the handler's too (<c>Forbidden</c>); an empty
/// id is neither — it is malformed input, so it is a 400 here rather than a pointless repository round trip.
/// </summary>
public sealed class GetTaskDetailQueryValidator : AbstractValidator<GetTaskDetailQuery>
{
    public GetTaskDetailQueryValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);
    }
}
