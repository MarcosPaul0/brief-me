using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.NotifyRelevantChange;

/// <summary>
/// Tells the people involved in a briefing that something relevant changed on it or on one of its tasks
/// (RF034, BRF-052). Reacts to <see cref="EntityChanged"/>, raised by the eight aggregate acts that qualify:
/// the briefing being edited, cancelled, completed or reopened, and a task being reassigned, moved between
/// statuses, replanned or reopened.
/// </summary>
/// <remarks>
/// One handler for the eight pairs rather than one per pair. <see cref="IDomainEventHandler{TEvent}"/> is
/// resolved by the concrete event type, so eight handlers would all run on every <see cref="EntityChanged"/>,
/// each discarding what is not its own — eight reads of the same briefing to produce one notice. What varies
/// between the pairs is the sentence, not the flow.
/// <para>
/// Runs after the commit that performed the change (BRF-049), so the fact it announces is already stored and
/// this reaction can neither undo it nor delay it. Writing the inbox entries — and committing them — is
/// <c>INotificationService</c>'s job, which is why no unit of work is injected here.
/// </para>
/// <para>
/// The decisions of the review (approve, reject, request changes) do <b>not</b> pass through here: they have
/// events of their own and their own handler (BRF-050), and <c>Briefing.RaiseChange</c> documents why a
/// second event for them would notify the same person twice. Likewise a first assignment, which is
/// <c>TaskAssigned</c> alone (RF033, BRF-051).
/// </para>
/// </remarks>
public sealed class NotifyInvolvedOnEntityChangedHandler(
    IBriefingRepository briefingRepository,
    IBriefingTaskRepository taskRepository,
    INotificationService notificationService) : IDomainEventHandler<EntityChanged>
{
    public async Task Handle(EntityChanged domainEvent, CancellationToken cancellationToken)
    {
        // The event carries BriefingId even when the subject is a task, precisely so the involved people can
        // be resolved from one read. A miss is an anomaly — the aggregate was committed moments ago — and it
        // returns instead of throwing: a handler has no Result to hand back, and an exception here would only
        // cost the delivery of the events queued behind it.
        var briefing = await briefingRepository.GetByIdAsync(domainEvent.BriefingId, cancellationToken);

        if (briefing is null)
            return;

        BriefingTask? task = null;

        if (domainEvent.Subject is DemandSubject.Task)
        {
            task = await taskRepository.GetByIdAsync(domainEvent.SubjectId, cancellationToken);

            // Unlike the missing briefing of BRF-051, which only cost the notice a clause, a missing task
            // here removes the subject of the sentence: there is nothing left to announce.
            if (task is null)
                return;
        }

        // Composed before the recipients are resolved: a pair with no wording must not pay for the task read
        // of the briefing branch, which is the most expensive step of the flow.
        var text = Compose(domainEvent, briefing, task);

        if (text is null)
            return;

        var recipients = await ResolveRecipientsAsync(domainEvent, briefing, task, cancellationToken);

        if (recipients.Count == 0)
            return;

        var (title, message) = text.Value;

        // One batch, not one call per person: the overload exists for exactly this requirement, so the three
        // entries are one write and one commit — and an invalid one leaves none of them written. Half of an
        // RF034 notification is worse than none.
        await notificationService.NotifyAsync(
            [
                .. recipients.Select(recipientId => new NotificationRequest(
                    recipientId,
                    NotificationEventType.EntityChanged,
                    title,
                    message))
            ],
            cancellationToken);
    }

    /// <summary>
    /// The sentence for one (subject, change) pair, or <c>null</c> for a pair nobody raises today.
    /// </summary>
    /// <remarks>
    /// The <c>{ } t</c> pattern on the task is what guarantees at compile time that no task branch reads a
    /// task that was never loaded. The final arm returns null rather than inventing a generic phrase: an act
    /// that ships without wording costs one notification, not a wrong one.
    /// </remarks>
    private static (string Title, string Message)? Compose(
        EntityChanged domainEvent,
        Briefing briefing,
        BriefingTask? task) =>
        (domainEvent.Subject, domainEvent.Change, task) switch
        {
            (DemandSubject.Briefing, EntityChangeKind.Edited, _) =>
                (BriefingNotifications.EditedTitle, BriefingNotifications.Edited(briefing.Title)),

            (DemandSubject.Briefing, EntityChangeKind.Cancelled, _) =>
                (BriefingNotifications.CancelledTitle, BriefingNotifications.Cancelled(briefing.Title)),

            (DemandSubject.Briefing, EntityChangeKind.Completed, _) =>
                (BriefingNotifications.CompletedTitle, BriefingNotifications.Completed(briefing.Title)),

            (DemandSubject.Briefing, EntityChangeKind.Reopened, _) =>
                (BriefingNotifications.ReopenedTitle, BriefingNotifications.Reopened(briefing.Title)),

            (DemandSubject.Task, EntityChangeKind.Reassigned, { } t) =>
                (BriefingTaskNotifications.ReassignedTitle,
                    BriefingTaskNotifications.Reassigned(t.Title, briefing.Title)),

            (DemandSubject.Task, EntityChangeKind.StatusChanged, { } t) =>
                (BriefingTaskNotifications.StatusChangedTitle,
                    BriefingTaskNotifications.StatusChanged(t.Title, briefing.Title, t.Status)),

            (DemandSubject.Task, EntityChangeKind.Replanned, { } t) =>
                (BriefingTaskNotifications.ReplannedTitle,
                    BriefingTaskNotifications.Replanned(t.Title, briefing.Title, t.Deadline, t.Priority)),

            (DemandSubject.Task, EntityChangeKind.Reopened, { } t) =>
                (BriefingTaskNotifications.ReopenedTitle,
                    BriefingTaskNotifications.Reopened(t.Title, briefing.Title)),

            _ => null
        };

    /// <summary>
    /// Who counts as "involved" (RF034): the requester, the Design Manager responsible for the briefing's
    /// current decision, and the assigned Designer(s) — the task's own when the change is on a task, every
    /// task's when the change is on the briefing.
    /// </summary>
    /// <remarks>
    /// "Manager responsible" is <see cref="Briefing.ReviewedById"/>, the reviewer of the current decision
    /// (RF038). While a briefing sits in <c>AwaitingApproval</c> with no earlier decision the field is null,
    /// and rightly so: the review queue is collective (<c>GetPendingApprovalAsync</c> is not scoped to a
    /// Manager), so there is no individual to name. Resolving the role through the
    /// <c>briefings.approve</c> permission instead would turn every status change of every task into a notice
    /// for every Manager in the company.
    /// <para>
    /// Scoping the Designers by subject follows RF057: tasks are independent, so the Designer of task A has
    /// nothing to do with a status change on task B.
    /// </para>
    /// <para>
    /// A <see cref="HashSet{T}"/> because the roles overlap — a requester who also designs is one person and
    /// gets one notice.
    /// </para>
    /// </remarks>
    private async Task<HashSet<Guid>> ResolveRecipientsAsync(
        EntityChanged domainEvent,
        Briefing briefing,
        BriefingTask? task,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid> { briefing.RequesterId };

        if (briefing.ReviewedById is { } reviewerId)
            recipients.Add(reviewerId);

        if (task is not null)
        {
            // On a transfer the assignee reached is the new owner, and TaskAssigned already tells them the
            // task is theirs (BRF-051) — adding them here would be two notices for one act. The previous
            // owner is not reachable: the event does not carry them and the reloaded task holds the new one.
            var announcesToAssignee = domainEvent.Change is not EntityChangeKind.Reassigned;

            if (announcesToAssignee && task.DesignerId is { } designerId)
                recipients.Add(designerId);
        }
        else
        {
            // Unpaginated by design (BRF-025). A briefing-level change only reaches here for acts that
            // happen once in a demand's life, and the alternative — a designers-of-a-briefing query — would
            // be a repository method with one caller.
            var tasks = await taskRepository.GetByBriefingIdAsync(briefing.Id, cancellationToken);

            foreach (var designerId in tasks.Select(briefingTask => briefingTask.DesignerId).OfType<Guid>())
                recipients.Add(designerId);
        }

        // Nobody is told about what they just did. The acts whose aggregate methods take no actor leave this
        // null (BRF-049 design decision #8), and the cost is one redundant message rather than a signature
        // change across six use cases.
        if (domainEvent.ChangedById is { } authorId)
            recipients.Remove(authorId);

        return recipients;
    }
}
