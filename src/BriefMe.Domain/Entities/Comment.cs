using BriefMe.Domain.Common;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

/// <summary>
/// A note left by someone involved in a task (RF021). Immutable once created (BRF-024): the conversation
/// trail on a task is append-only, so a correction is a new comment, never an edit. Child entity of the
/// <see cref="BriefingTask"/> aggregate.
/// </summary>
public class Comment : Entity
{
    public const int MaxTextLength = 4000;

    private Comment(Guid id, Guid taskId, Guid authorId, string text, DateTime createdAt) : base(id)
    {
        TaskId = taskId;
        AuthorId = authorId;
        Text = text;
        CreatedAt = createdAt;
    }

    private Comment()
    {
    }

    public Guid TaskId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public static Comment Create(Guid taskId, Guid authorId, string? text)
    {
        if (taskId == Guid.Empty)
            throw new DomainException("Comment task is required.");

        if (authorId == Guid.Empty)
            throw new DomainException("Comment author is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Comment text is required.");

        var normalized = text.Trim();

        if (normalized.Length > MaxTextLength)
            throw new DomainException($"Comment text must be at most {MaxTextLength} characters.");

        return new Comment(Guid.NewGuid(), taskId, authorId, normalized, DateTime.UtcNow);
    }
}
