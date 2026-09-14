using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.NotifyOverdue;

/// <summary>
/// Tells the people who answer for a demand that it missed its deadline (RF035, BRF-053). Reacts to
/// <see cref="DeadlineOverdue"/>, raised by the two aggregate acts the SLA monitor performs on its sweep
/// (RF028, BRF-043): <see cref="BriefingTask.MarkOverdue"/> and <see cref="Briefing.MarkOverdue"/>.
/// </summary>
/// <remarks>
/// One handler for both kinds of demand rather than one per kind. <see cref="IDomainEventHandler{TEvent}"/>
/// is resolved by the concrete event type, so two handlers would both run on every
/// <see cref="DeadlineOverdue"/>, each discarding what is not its own. <see cref="DemandSubject"/> exists so
/// the consumer can branch inside one flow — its own documentation says both consumers (BRF-052, BRF-053)
/// ask the same question of it.
/// <para>
/// Runs after the commit that stored the mark (BRF-049), so the breach it announces is already a fact in the
/// database — <c>MonitorDeadlinesCommandHandler</c> records that this ordering is why "a notification can
/// never describe a breach that is not yet stored". Writing the inbox entries — and committing them — is
/// <c>INotificationService</c>'s job, which is why no unit of work is injected here.
/// </para>
/// <para>
/// Fires <b>once</b> per breach, and that is a property of the sweep rather than of this handler: the
/// monitor's query filters <c>OverdueSince IS NULL</c>, so a marked demand is never judged again. Replanning
/// clears the mark (<see cref="BriefingTask.SetDeadlineAndPriority"/>, <see cref="Briefing.Edit"/>) and a
/// second breach is a second event — which is the point. A recurring "still late" reminder would need a
/// different trigger, a sweep of the already-marked, and no requirement asks for one.
/// </para>
/// </remarks>
public sealed class NotifyResponsiblesOnDeadlineOverdueHandler(
    IBriefingRepository briefingRepository,
    IBriefingTaskRepository taskRepository,
    INotificationService notificationService) : IDomainEventHandler<DeadlineOverdue>
{
    public async Task Handle(DeadlineOverdue domainEvent, CancellationToken cancellationToken)
    {
        // The event carries BriefingId even when the subject is a task, precisely so the demand can be named
        // and the Manager resolved from one read. A miss is an anomaly — the aggregate was committed moments
        // ago — and it returns instead of throwing: a handler has no Result to hand back, and one sweep
        // queues one event per marked demand, so an exception here would cost the alert of all the others.
        var briefing = await briefingRepository.GetByIdAsync(domainEvent.BriefingId, cancellationToken);

        if (briefing is null)
            return;

        BriefingTask? task = null;

        if (domainEvent.Subject is DemandSubject.Task)
        {
            task = await taskRepository.GetByIdAsync(domainEvent.SubjectId, cancellationToken);

            // A missing task removes the subject of the sentence: there is nothing left to announce. Same
            // choice as BRF-052, and unlike BRF-051, where a missing briefing only cost the notice a clause.
            if (task is null)
                return;
        }

        var text = Compose(domainEvent, briefing, task);

        if (text is null)
            return;

        var recipients = ResolveRecipients(domainEvent, briefing);

        if (recipients.Count == 0)
            return;

        var (title, message) = text.Value;

        // One batch, not one call per person: the overload exists for exactly this, so both entries are one
        // write and one commit — and an invalid one leaves neither written. Half of an overdue notice is
        // worse than none: the Manager would escalate a delay the Designer never saw.
        await notificationService.NotifyAsync(
            [
                .. recipients.Select(recipientId => new NotificationRequest(
                    recipientId,
                    NotificationEventType.DeadlineOverdue,
                    title,
                    message))
            ],
            cancellationToken);
    }

    /// <summary>
    /// The sentence for the late demand, or <c>null</c> for a subject nobody raises today.
    /// </summary>
    /// <remarks>
    /// The <c>{ } t</c> pattern is what guarantees at compile time that the task branch cannot read a task
    /// that was never loaded. The final arm is unreachable today — the early return above already covers the
    /// missing task — and exists so that a new <see cref="DemandSubject"/> shipped without wording costs one
    /// notification rather than a wrong one.
    /// <para>
    /// Composed before the recipients are resolved, for symmetry with the sibling handler of BRF-052. Here
    /// the order buys little: neither branch needs an extra read to resolve the recipients.
    /// </para>
    /// </remarks>
    private static (string Title, string Message)? Compose(
        DeadlineOverdue domainEvent,
        Briefing briefing,
        BriefingTask? task) =>
        (domainEvent.Subject, task) switch
        {
            (DemandSubject.Briefing, _) =>
                (BriefingNotifications.OverdueTitle,
                    BriefingNotifications.Overdue(briefing.Title, domainEvent.DueAtUtc)),

            (DemandSubject.Task, { } t) =>
                (BriefingTaskNotifications.OverdueTitle,
                    BriefingTaskNotifications.Overdue(t.Title, briefing.Title, domainEvent.DueAtUtc)),

            _ => null
        };

    /// <summary>
    /// Who answers for the late demand (RF035, which says "responsáveis" in the plural): the owner of the
    /// demand and the Design Manager who approved the briefing.
    /// </summary>
    /// <remarks>
    /// The owner is <see cref="DeadlineOverdue.ResponsibleId"/>, which the event itself defines as the
    /// assigned Designer for a task (RF017) and the requester for a briefing — so this handler never has to
    /// decide it from the subject. It is null for a task nobody has been given yet, and that is exactly the
    /// case the Manager covers: a planned, unassigned task already past its deadline is the one delay that
    /// would otherwise be announced to nobody.
    /// <para>
    /// The Manager is <see cref="Briefing.ReviewedById"/>, the reviewer of the current decision (RF038) —
    /// the same translation of "Manager responsible for approval" that BRF-052 made. It is null while a
    /// briefing sits in <c>AwaitingApproval</c> with no earlier decision, and rightly so: the review queue
    /// is collective (<c>GetPendingApprovalAsync</c> is not scoped to a Manager), so there is no individual
    /// to name.
    /// </para>
    /// <para>
    /// The Manager is included on every breach, not only when the owner is missing. Adding them by exception
    /// would make their alert depend on an accident — the task happening to be unassigned — instead of on
    /// their role, and would hide the delays of the tasks that <i>do</i> have an owner, which are most of
    /// them. The noise is bounded by construction: one notice per breach, not one per sweep of the job.
    /// </para>
    /// <para>
    /// No author is filtered out, unlike BRF-052: a delay is nobody's act. The sweep runs with no
    /// <c>ICurrentUser</c> at all.
    /// </para>
    /// <para>
    /// A <see cref="HashSet{T}"/> because the two roles can be the same person — a Manager who also carries
    /// the task is one person and gets one notice.
    /// </para>
    /// </remarks>
    private static HashSet<Guid> ResolveRecipients(DeadlineOverdue domainEvent, Briefing briefing)
    {
        var recipients = new HashSet<Guid>();

        if (domainEvent.ResponsibleId is { } responsibleId)
            recipients.Add(responsibleId);

        if (briefing.ReviewedById is { } reviewerId)
            recipients.Add(reviewerId);

        return recipients;
    }
}
