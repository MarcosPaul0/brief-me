using BriefMe.Domain.Entities;

namespace BriefMe.Domain.Repositories;

/// <summary>
/// Access to the in-app inbox (RF031–RF035). Defined in <c>Domain</c> per the project's Repository pattern,
/// and introduced by the slice that first persists a notification (BRF-048), as
/// <see cref="ISlaRuleRepository"/> was by BRF-042.
/// </summary>
/// <remarks>
/// Every read is scoped by recipient — there is no "all notifications" method and no way to ask for someone
/// else's inbox, because there is no caller that legitimately wants one: the bell of BRF-054 reads the
/// authenticated person's own rows, and the recipient is never an input the caller supplies.
/// <para>
/// All four reads are served by the index the mapping declares, <c>(RecipientId, ReadAt)</c>: the listing
/// and the total by its leftmost column, the unread count and set by both.
/// </para>
/// </remarks>
public interface INotificationRepository : IRepository<Notification>
{
    /// <summary>
    /// Adds a batch in one go, as RF034 needs when a change concerns the requester, the assigned Designer
    /// and the Manager at once (BRF-052): one write and one commit instead of one round trip per person.
    /// An empty collection is valid and adds nothing.
    /// </summary>
    Task AddRangeAsync(
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of a person's inbox, newest first (BRF-054) — read and unread alike, because the bell shows
    /// what happened, not only what is new.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetByRecipientAsync(
        Guid recipientId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Total behind <see cref="GetByRecipientAsync"/>, so the handler can assemble the page envelope.</summary>
    Task<int> CountByRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many entries the person has not read yet — the bell's badge (BRF-054). A count rather than the
    /// rows: the badge needs the number, and polling it every minute must not carry an inbox with it.
    /// </summary>
    Task<int> CountUnreadByRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The person's unread entries, so opening the bell can mark them through
    /// <see cref="Notification.MarkAsRead"/> rather than by writing <c>ReadAt</c> from outside the aggregate.
    /// Unpaginated on purpose: the gesture is "mark everything", and a page of it would leave a badge over
    /// an emptied panel.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetUnreadByRecipientAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default);
}
