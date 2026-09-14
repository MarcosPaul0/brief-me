namespace BriefMe.Application.Abstractions.Notifications;

/// <summary>
/// Tells people things (RF031–RF035, BRF-047). The single door the notification handlers of BRF-050–053
/// knock on; how the message reaches the person — in-app inbox today, e-mail or push later — is behind it.
/// </summary>
/// <remarks>
/// The acceptance criterion of BRF-047 is that adding e-mail must not change this contract, which is why
/// nothing here names a channel: a caller states <i>who</i> to tell and <i>what</i>, and the implementation
/// decides through which channels that goes. Adding one is a new channel behind the same method, in the
/// shape <see cref="IFileStorageService"/> hides local disk and MinIO from every caller (BRF-011).
/// <para>
/// Returns no <c>Result</c>. Its callers are domain event handlers with nobody waiting on an answer — the
/// same asymmetry that keeps <see cref="Messaging.IDomainEventHandler{TEvent}"/> out of the request/response
/// pipeline. A delivery that cannot happen is the implementation's problem to log; it must never surface as
/// a failure of the use case that raised the event, which is already committed.
/// </para>
/// <para>
/// The implementation owns its own persistence and commit. It runs after the dispatch of BRF-049, which
/// happens <b>after</b> the originating commit, so there is no ambient unit of work left for it to enlist
/// in. Delivered with the inbox persistence of BRF-048.
/// </para>
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// Delivers one notification. Recording it in the recipient's in-app inbox is the guaranteed part;
    /// further channels are additive.
    /// </summary>
    Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers a batch, as RF034 needs when a change concerns the requester, the assigned Designer and the
    /// Manager at once (BRF-052). One call so the batch is one write and one commit, instead of one round
    /// trip per person; an empty collection is valid and does nothing.
    /// </summary>
    Task NotifyAsync(
        IReadOnlyCollection<NotificationRequest> requests,
        CancellationToken cancellationToken = default);
}
