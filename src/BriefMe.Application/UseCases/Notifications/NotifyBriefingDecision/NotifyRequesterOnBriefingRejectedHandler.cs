using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.NotifyBriefingDecision;

/// <summary>
/// Tells the requester that their briefing was rejected, and why (RF032 with RF013, BRF-050). Reacts to
/// <see cref="BriefingRejected"/>, raised by the Manager's rejection (RF012, BRF-017).
/// </summary>
/// <remarks>
/// Twin of <see cref="NotifyRequesterOnBriefingApprovedHandler"/> — same post-commit position, same silent
/// return on a missing briefing — with one difference: the justification comes from the event itself, which
/// carries it precisely so this handler does not have to read the aggregate for it.
/// </remarks>
public sealed class NotifyRequesterOnBriefingRejectedHandler(
    IBriefingRepository briefingRepository,
    INotificationService notificationService) : IDomainEventHandler<BriefingRejected>
{
    public async Task Handle(BriefingRejected domainEvent, CancellationToken cancellationToken)
    {
        var briefing = await briefingRepository.GetByIdAsync(domainEvent.BriefingId, cancellationToken);

        if (briefing is null)
            return;

        await notificationService.NotifyAsync(
            new NotificationRequest(
                domainEvent.RequesterId,
                NotificationEventType.BriefingRejected,
                BriefingNotifications.RejectedTitle,
                BriefingNotifications.Rejected(briefing.Title, domainEvent.Justification)),
            cancellationToken);
    }
}
