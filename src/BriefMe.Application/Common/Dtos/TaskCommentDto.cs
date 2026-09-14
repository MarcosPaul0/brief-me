using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of one line of a task's comment trail as the detail screen renders it (RF021, BRF-034).
/// Distinct from <see cref="CommentDto"/>, which is the write use case's echo of the comment the caller has
/// just left: there the author is the caller and needs no name, here the reader is looking at someone
/// else's words and an <c>AuthorId</c> alone says nothing.
/// </summary>
/// <remarks>
/// <see cref="AuthorName"/> is nullable because the lookup may not resolve — the author was removed or
/// soft-deleted. That degrades one cell; the comment itself stays readable, which is the point of an
/// append-only trail (BRF-024). <see cref="AuthorId"/> travels alongside so the reader can still be
/// identified by whatever else knows the id.
/// </remarks>
public sealed record TaskCommentDto(
    Guid Id,
    Guid AuthorId,
    string? AuthorName,
    string Text,
    DateTime CreatedAt)
{
    public static TaskCommentDto FromCommentWithAuthorName(
        Comment comment,
        IReadOnlyDictionary<Guid, string> authorNames)
    {
        return new(
            comment.Id,
            comment.AuthorId,
            authorNames.GetValueOrDefault(comment.AuthorId),
            comment.Text,
            comment.CreatedAt);
    }
}
