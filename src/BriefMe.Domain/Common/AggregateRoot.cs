namespace BriefMe.Domain.Common;

/// <summary>
/// Marks an entity that is the consistency boundary of its own aggregate — the only kind a repository loads
/// and saves directly.
/// </summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }
}
