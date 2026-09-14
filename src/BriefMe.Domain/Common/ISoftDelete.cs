namespace BriefMe.Domain.Common;

public interface ISoftDelete
{
    bool IsDeleted { get; }

    DateTime? DeletedAt { get; }

    string? DeletedBy { get; }

    void MarkAsDeleted(string? deletedBy);
}
