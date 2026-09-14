using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence;

/// <summary>
/// Commits the tracked changes and publishes the domain events the aggregates raised while producing them
/// (BRF-049) — the single publishing pipeline of the system: no use case calls
/// <see cref="IDomainEventDispatcher"/> itself, and none has to remember to.
/// </summary>
/// <remarks>
/// The hook lives here rather than in a <c>SaveChangesInterceptor</c> because handlers of this domain commit
/// (<c>CompleteBriefingWhenAllTasksApprovedHandler</c> closes the briefing, <c>InAppNotificationService</c>
/// writes the inbox row): an interceptor's <c>SavedChangesAsync</c> runs <b>inside</b> the SaveChanges call,
/// so their commit would reenter the same <see cref="DbContext"/> mid-save. Here the dispatch is plainly
/// after it, off that stack — and <see cref="CommitAsync"/> is the only caller of <c>SaveChangesAsync</c> in
/// the application, so covering it covers every use case.
/// <para>
/// A nested commit from a handler collects only what that handler raised: the dispatcher clears an
/// aggregate's events before invoking the first reaction (BRF-043), so no pass can re-publish what woke it.
/// </para>
/// </remarks>
public class UnitOfWork(AppDbContext dbContext, IDomainEventDispatcher eventDispatcher) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Collected before writing: an aggregate deleted in this same commit stops being tracked once
        // SaveChanges accepts the changes, and its pending events would be lost with it.
        var aggregates = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        await dbContext.SaveChangesAsync(cancellationToken);

        // Strictly after the write, and skipped entirely when nothing was raised — the steady state of any
        // commit whose aggregates announced nothing. A failing write throws above this line, so nobody is
        // ever told about a fact the database does not hold.
        if (aggregates.Count > 0)
            await eventDispatcher.DispatchAsync(aggregates, cancellationToken);
    }
}
