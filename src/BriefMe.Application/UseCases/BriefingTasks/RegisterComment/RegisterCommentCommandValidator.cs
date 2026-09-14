using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Entities;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.RegisterComment;

/// <summary>
/// Presence and size validation for the comment command (BRF-030). Unlike <c>Justification</c> — a value
/// object that answers with a <c>Result</c> — <c>Comment.Create</c> guards its invariant with a
/// <c>DomainException</c>, so without these rules an empty text (plain user input, an expected failure) would
/// surface as a <c>500</c>. The aggregate keeps throwing: this is the outer of two lines of defense, not a
/// replacement for it.
/// </summary>
public sealed class RegisterCommentCommandValidator : AbstractValidator<RegisterCommentCommand>
{
    public RegisterCommentCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.TaskRequired);

        // Length is measured on the raw text while Comment.Create measures it trimmed — the two only disagree
        // for a text already over the limit that also carries padding, where failing is the safer answer.
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage(BriefingTaskErrors.Validation.CommentTextRequired)
            .MaximumLength(Comment.MaxTextLength)
            .WithMessage(BriefingTaskErrors.Validation.CommentTextTooLong(Comment.MaxTextLength));
    }
}
