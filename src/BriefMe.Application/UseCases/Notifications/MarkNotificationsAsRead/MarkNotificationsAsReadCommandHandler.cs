using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.MarkNotificationsAsRead;

/// <summary>
/// Applies the read mark to the caller's whole unread inbox (BRF-054), through the entity and in one commit.
/// </summary>
/// <remarks>
/// Loads the rows and calls <c>Notification.MarkAsRead()</c> on each rather than issuing a bulk
/// <c>UPDATE … WHERE ReadAt IS NULL</c>. The mark is the aggregate's own state and its idempotency — the
/// first read instant is kept, so opening the bell twice does not rewrite when it happened — is written once,
/// in the entity; a set-based update would keep a second copy of that rule in SQL, where nothing tests it.
/// The volume is one person's unread inbox, selected directly by the <c>(RecipientId, ReadAt)</c> index.
/// <para>
/// An empty set is success, not a failure: opening a bell with nothing new is the ordinary case, and it
/// commits nothing rather than paying for a transaction that would write no row.
/// </para>
/// </remarks>
public sealed class MarkNotificationsAsReadCommandHandler(
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<MarkNotificationsAsReadCommand>
{
    public async Task<Result> Handle(
        MarkNotificationsAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var recipientId))
            return Result.Unauthorized(CommonErrors.UserNotAuthenticated);

        var unread = await notificationRepository.GetUnreadByRecipientAsync(recipientId, cancellationToken);

        if (unread.Count == 0)
            return Result.Success();

        foreach (var notification in unread)
        {
            notification.MarkAsRead();

            // Explicit, though the instances come back tracked: the contract exposes Update, and relying on
            // change detection would make this handler correct only while the repository keeps returning
            // tracked entities.
            notificationRepository.Update(notification);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
