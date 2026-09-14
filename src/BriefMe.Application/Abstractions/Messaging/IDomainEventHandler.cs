using BriefMe.Domain.Common;

namespace BriefMe.Application.Abstractions.Messaging;

/// <summary>
/// Reacts to a domain event raised by an aggregate. The consumption side of the publishing pipeline of
/// BRF-049: <c>UnitOfWork</c> collects <see cref="AggregateRoot.DomainEvents"/> and dispatches them after the
/// commit, so a handler always runs against state the database already holds — and may commit its own.
/// </summary>
/// <remarks>
/// Deliberately not an <see cref="IRequestHandler{TRequest,TResponse}"/>: a request has one handler and a
/// caller waiting for its <c>Result</c>; an event has zero or more handlers and nobody to answer. A reaction
/// that fails does so against the dispatcher, not against the use case that raised the event.
/// <para>
/// Contravariant in <typeparamref name="TEvent"/> so the dispatcher can resolve
/// <c>IDomainEventHandler&lt;TConcrete&gt;</c> from the runtime type of the event it holds.
/// </para>
/// </remarks>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
