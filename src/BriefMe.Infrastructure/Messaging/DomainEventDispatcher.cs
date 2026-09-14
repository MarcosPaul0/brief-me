using System.Collections.Concurrent;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BriefMe.Infrastructure.Messaging;

/// <summary>
/// Delivers domain events to every handler registered for their concrete type (BRF-043, wired to every use
/// case by the commit of BRF-049). Lives in Infrastructure because resolving handlers by a runtime type is a
/// container concern.
/// </summary>
/// <remarks>
/// Events are collected and cleared <b>before</b> the first handler runs: a reaction that ends up raising
/// events on the same aggregate must not have its own events re-delivered by the pass that triggered it.
/// <para>
/// A failing handler is logged and the remaining deliveries continue. The dispatcher is called after the
/// commit, so the state the event describes is already stored — letting one reaction throw would abort the
/// delivery of every event after it without undoing anything, and would surface inside the caller's
/// <c>CommitAsync</c> as if the write had failed.
/// </para>
/// </remarks>
public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    // Keyed by the concrete event type: one wrapper per type, built once for the life of the process. Same
    // device as Application/Messaging/Sender, and for the same reason — the alternative is reflecting over
    // MethodInfo on every single delivery.
    private static readonly ConcurrentDictionary<Type, DomainEventHandlerWrapperBase> Wrappers = new();

    public async Task DispatchAsync(
        IReadOnlyCollection<AggregateRoot> aggregates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            var wrapper = Wrappers.GetOrAdd(domainEvent.GetType(), CreateWrapper);

            try
            {
                await wrapper.Handle(domainEvent, serviceProvider, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Handler for domain event {DomainEvent} failed; the remaining events are still dispatched.",
                    domainEvent.GetType().Name);
            }
        }
    }

    private static DomainEventHandlerWrapperBase CreateWrapper(Type eventType)
    {
        var wrapperType = typeof(DomainEventHandlerWrapper<>).MakeGenericType(eventType);
        return (DomainEventHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
    }
}

internal abstract class DomainEventHandlerWrapperBase
{
    public abstract Task Handle(
        IDomainEvent domainEvent,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Closes <see cref="IDomainEventHandler{TEvent}"/> over the concrete event type so the handlers can be
/// resolved and invoked through a normal, typed call instead of reflection on every delivery.
/// </summary>
internal sealed class DomainEventHandlerWrapper<TEvent> : DomainEventHandlerWrapperBase
    where TEvent : IDomainEvent
{
    public override async Task Handle(
        IDomainEvent domainEvent,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedEvent = (TEvent)domainEvent;

        // GetServices, not GetRequiredService: an event legitimately has zero handlers — which is the state
        // of DeadlineOverdue until BRF-053 — and may have several.
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
            await handler.Handle(typedEvent, cancellationToken);
    }
}
