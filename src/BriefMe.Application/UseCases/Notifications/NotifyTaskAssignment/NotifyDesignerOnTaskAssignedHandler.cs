using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.NotifyTaskAssignment;

/// <summary>
/// Tells a Designer that a task is theirs (RF033, BRF-051). Reacts to <see cref="TaskAssigned"/>, raised both
/// by the first assignment (RF017, BRF-027) and by a transfer (RF026, BRF-038) — what the new owner needs to
/// know does not depend on how the task reached them, which is why the aggregate raises one event for both.
/// </summary>
/// <remarks>
/// Runs after the commit that assigned the task (BRF-049), so the fact it announces is already stored and
/// this reaction can neither undo it nor delay it. Writing the inbox entry — and committing it — is
/// <c>INotificationService</c>'s job, which is why no unit of work is injected here.
/// <para>
/// Two reads, both for one field each: the event carries ids, not text. Copying the titles into the event
/// instead would duplicate aggregate state that ages the moment either is renamed.
/// </para>
/// <para>
/// The transfer also raises <c>EntityChanged</c> for the requester and the Manager (RF034, BRF-052). That is
/// a separate audience on a separate event; this handler knows nothing about it and the order between the
/// two does not matter.
/// </para>
/// </remarks>
public sealed class NotifyDesignerOnTaskAssignedHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    INotificationService notificationService) : IDomainEventHandler<TaskAssigned>
{
    public async Task Handle(TaskAssigned domainEvent, CancellationToken cancellationToken)
    {
        var task = await taskRepository.GetByIdAsync(domainEvent.TaskId, cancellationToken);

        // The event carries the id of the aggregate whose commit published it, so a miss is an anomaly.
        // Returns instead of throwing: a handler has no Result to hand back, and an exception here would
        // only cost the delivery of the events queued behind it.
        if (task is null)
            return;

        // A missing briefing degrades the sentence instead of cancelling the notice — losing one clause is
        // a smaller harm than losing the alert, the same trade the DTO factories make with GetValueOrDefault.
        var briefing = await briefingRepository.GetByIdAsync(domainEvent.BriefingId, cancellationToken);

        // DesignerId comes from the event, not from the reloaded task: it is who the assignment was made to,
        // even if a later transfer already moved the task on between that commit and this dispatch.
        await notificationService.NotifyAsync(
            new NotificationRequest(
                domainEvent.DesignerId,
                NotificationEventType.TaskAssigned,
                BriefingTaskNotifications.AssignedTitle,
                BriefingTaskNotifications.Assigned(task.Title, briefing?.Title)),
            cancellationToken);
    }
}
