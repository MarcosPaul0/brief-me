using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;

/// <summary>
/// Presence and format validation of the batch (BRF-026). The status invariant is a domain rule checked by
/// the handler and by <c>Briefing.StartExecution()</c>; here we only make sure a malformed batch fails as a
/// validation error (400) instead of a <c>DomainException</c> from <c>BriefingTask.Create</c> (500).
/// </summary>
public sealed class CreateBriefingTasksCommandValidator : AbstractValidator<CreateBriefingTasksCommand>
{
    /// <summary>Matches the <c>varchar(200)</c> of <c>Tasks.Title</c> (BriefingTaskConfiguration).</summary>
    private const int TitleMaxLength = 200;

    public CreateBriefingTasksCommandValidator()
    {
        RuleFor(x => x.BriefingId)
            .NotEmpty().WithMessage(BriefingErrors.Validation.BriefingRequired);

        // RF015 is "one or more tasks" — never zero.
        RuleFor(x => x.Tasks)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.AtLeastOneTaskRequired);

        // ChildRules so a failure names the offending item (Tasks[1].Title), not just the collection.
        RuleForEach(x => x.Tasks).ChildRules(task =>
        {
            task.RuleFor(t => t.Title)
                .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TitleRequired)
                .MaximumLength(TitleMaxLength)
                .WithMessage(BriefingTaskErrors.Validation.TitleTooLong(TitleMaxLength));

            task.RuleFor(t => t.Priority)
                .IsInEnum().WithMessage(BriefingTaskErrors.Validation.InvalidPriority);
        });
    }
}
