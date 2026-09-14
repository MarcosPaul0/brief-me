using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.NotifyBriefingDecision;

/// <summary>
/// Tells the requester that their briefing was approved (RF031, BRF-050). Reacts to
/// <see cref="BriefingApproved"/>, raised by the Manager's approval (RF011, BRF-016).
/// </summary>
/// <remarks>
/// Runs after the commit that approved the briefing (BRF-049), so the fact it announces is already stored
/// and this reaction can neither undo it nor delay it. Writing the inbox entry — and committing it — is
/// <c>INotificationService</c>'s job, which is why no unit of work is injected here.
/// <para>
/// Reloads the briefing for one field, its title: the event carries ids only, and an alert that does not
/// name the demand it speaks of is not worth sending. Copying the title into the event instead would
/// duplicate aggregate state that ages the moment the briefing is edited.
/// </para>
/// </remarks>
public sealed class NotifyRequesterOnBriefingApprovedHandler(
    IBriefingRepository briefingRepository,
    INotificationService notificationService) : IDomainEventHandler<BriefingApproved>
{
    public async Task Handle(BriefingApproved domainEvent, CancellationToken cancellationToken)
    {
        var briefing = await briefingRepository.GetByIdAsync(domainEvent.BriefingId, cancellationToken);

        // The event carries the id of the aggregate whose commit published it, so a miss is an anomaly.
        // Returns instead of throwing: a handler has no Result to hand back, and an exception here would
        // only cost the delivery of the events queued behind it.
        if (briefing is null)
            return;

        // RequesterId comes from the event, not from the reloaded briefing: it is who was the requester when
        // the decision was taken, which is the person the decision concerns.
        await notificationService.NotifyAsync(
            new NotificationRequest(
                domainEvent.RequesterId,
                NotificationEventType.BriefingApproved,
                BriefingNotifications.ApprovedTitle,
                BriefingNotifications.Approved(briefing.Title)),
            cancellationToken);
    }
}
