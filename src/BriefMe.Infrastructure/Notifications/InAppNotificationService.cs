using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Infrastructure.Notifications;

/// <summary>
/// Delivers notifications to the recipient's in-app inbox (RF031–RF035, BRF-048): the implementation of
/// <see cref="INotificationService"/> the handlers of BRF-050–053 reach without knowing it exists.
/// </summary>
/// <remarks>
/// Named for its channel, not for the contract. Adding e-mail later is a second class behind the same
/// interface, and the name <c>NotificationService</c> is left free for the composite that would fan out to
/// both — the shape <see cref="Application.Abstractions.IFileStorageService"/> has with its local and MinIO
/// implementations (BRF-011).
/// <para>
/// Commits on its own. Its callers run after the dispatch of BRF-049, which happens after the originating
/// use case has already committed, so there is no ambient unit of work left to enlist in. That is also the
/// order worth having: the alert goes out only once the fact it describes is in the database, and an alert
/// that fails undoes no approval.
/// </para>
/// </remarks>
public sealed class InAppNotificationService(
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : INotificationService
{
    public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return NotifyAsync([request], cancellationToken);
    }

    public async Task NotifyAsync(
        IReadOnlyCollection<NotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
            return;

        // Every entry is built before the first one is added: a DomainException on the third request then
        // happens while the context is still untouched, so a batch that cannot be written in full writes
        // nothing. Half of an RF034 notification is worse than none — the Manager would know of a change the
        // requester never saw.
        var notifications = requests
            .Select(request => Notification.Create(
                request.RecipientId,
                request.EventType,
                request.Title,
                request.Message))
            .ToList();

        await notificationRepository.AddRangeAsync(notifications, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);
    }
}
