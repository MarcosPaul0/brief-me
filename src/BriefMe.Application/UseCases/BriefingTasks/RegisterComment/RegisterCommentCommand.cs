using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.BriefingTasks.RegisterComment;

/// <summary>
/// Registers a comment on an existing task (RF021, BRF-030). The endpoint behind it is
/// <c>POST /tasks/{id}/comments</c> (BRF-032).
/// </summary>
/// <remarks>
/// The author is not part of the command: it is the authenticated user (<c>ICurrentUser</c>), which the
/// handler reads. Carrying an author id in the payload would let one involved person comment as another.
/// <see cref="Text"/> is nullable because that is what an absent JSON field deserializes to — the validator
/// turns it into a <c>400</c> instead of letting <c>Comment.Create</c> raise a <c>DomainException</c>.
/// </remarks>
public sealed record RegisterCommentCommand(Guid TaskId, string? Text) : ICommand<CommentDto>;
