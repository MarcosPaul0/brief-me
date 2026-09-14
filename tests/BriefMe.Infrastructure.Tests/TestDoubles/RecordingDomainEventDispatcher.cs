using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Common;

namespace BriefMe.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Stands in for the real dispatcher in the tests of <c>UnitOfWork</c> (BRF-049): records what each commit
/// handed it, and optionally runs a reaction — enough to observe order, nesting and re-delivery without
/// bringing in a container. Delivery itself is covered by <c>DomainEventDispatcherTests</c>.
/// </summary>
public sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly Func<IReadOnlyCollection<IDomainEvent>, Task>? _onDispatch;

    public RecordingDomainEventDispatcher(Func<IReadOnlyCollection<IDomainEvent>, Task>? onDispatch = null)
    {
        _onDispatch = onDispatch;
    }

    public int Calls { get; private set; }

    public List<IDomainEvent> DispatchedEvents { get; } = [];

    public async Task DispatchAsync(
        IReadOnlyCollection<AggregateRoot> aggregates,
        CancellationToken cancellationToken = default)
    {
        Calls++;

        var events = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
        DispatchedEvents.AddRange(events);

        // Clearing before reacting is the real dispatcher's contract (BRF-043), and what keeps a handler's
        // own commit from re-publishing the event that woke it.
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        if (_onDispatch is not null)
            await _onDispatch(events);
    }
}
