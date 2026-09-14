using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Output representation of a comment left on a task (RF021, BRF-030). Carries the author and the moment
/// because that is what makes an append-only trail readable: a correction is a new comment, never an edit
/// (BRF-024), so the reader needs to know who said what and when.
/// </summary>
/// <remarks>
/// <c>AuthorId</c> is an id, not a name: resolving display names is the reading side's job (BRF-034), which
/// loads a page of comments at once and can look the users up in one query — the same shape as
/// <c>PendingApprovalBriefingDto</c>.
/// </remarks>
public sealed record CommentDto(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string Text,
    DateTime CreatedAt)
{
    public static CommentDto FromComment(Comment comment)
    {
        return new(
            comment.Id,
            comment.TaskId,
            comment.AuthorId,
            comment.Text,
            comment.CreatedAt);
    }
}
