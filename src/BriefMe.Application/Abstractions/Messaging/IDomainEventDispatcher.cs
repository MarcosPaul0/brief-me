using BriefMe.Domain.Common;

namespace BriefMe.Application.Abstractions.Messaging;

/// <summary>
/// Delivers the events aggregates raised to every <see cref="IDomainEventHandler{TEvent}"/> registered for
/// them. The publishing half of the pipeline of BRF-049; its only caller is <c>UnitOfWork</c>, which invokes
/// it after every commit that has something to announce.
/// </summary>
/// <remarks>
/// Takes aggregates rather than events on purpose: the dispatch happens right after <c>SaveChanges</c>, where
/// what is at hand are <c>ChangeTracker</c> entries, and the dispatcher — not each caller — owns collecting
/// <see cref="AggregateRoot.DomainEvents"/> and calling <see cref="AggregateRoot.ClearDomainEvents"/>.
/// <para>
/// Always called <b>after</b> the commit: the state the events describe is already a fact of the database,
/// so a reaction that fails is the reaction's problem, never a reason to undo the write. Delivery is
/// therefore at least once — the "exactly once" of BRF-043's acceptance criterion comes from the monitor's
/// query never re-reading a demand it already marked, not from this contract.
/// </para>
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Publishes every pending event of <paramref name="aggregates"/> and clears them. Aggregates with no
    /// pending event cost nothing, so callers may pass whatever they touched.
    /// </summary>
    Task DispatchAsync(
        IReadOnlyCollection<AggregateRoot> aggregates,
        CancellationToken cancellationToken = default);
}
