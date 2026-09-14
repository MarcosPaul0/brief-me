using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.GetMyNotifications;

/// <summary>
/// Reads the caller's inbox (RF031–RF035, BRF-054): one page of their notifications, newest first, each with
/// the sentence it was written with and whether it had already been read.
/// </summary>
/// <remarks>
/// One repository per page plus its count, and nothing else: unlike the task and briefing lists, no display
/// name has to be resolved, because a notification stores the text it was created with rather than pointing
/// at the aggregate it speaks of (BRF-047, decision #2). The list of ten costs two queries whatever it says.
/// <para>
/// Ordering is the repository's, and is not restated here — two definitions of "newest first" would drift at
/// the first page boundary, the same reason <c>GetTasksByDesignerQueryHandler</c> leaves its own ordering
/// alone.
/// </para>
/// </remarks>
public sealed class GetMyNotificationsQueryHandler(
    INotificationRepository notificationRepository,
    ICurrentUser currentUser) : IQueryHandler<GetMyNotificationsQuery, PagedDto<NotificationDto>>
{
    public async Task<Result<PagedDto<NotificationDto>>> Handle(
        GetMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        // The caller is the filter, so an unauthenticated context has nothing to read and must never reach
        // the repository with an empty recipient id — which would match no row, but by accident.
        if (!Guid.TryParse(currentUser.UserId, out var recipientId))
            return Result.Unauthorized<PagedDto<NotificationDto>>(CommonErrors.UserNotAuthenticated);

        var notifications = await notificationRepository.GetByRecipientAsync(
            recipientId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await notificationRepository.CountByRecipientAsync(recipientId, cancellationToken);

        var items = notifications
            .Select(NotificationDto.FromNotification)
            .ToList();

        return Result.Success(PagedDto<NotificationDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }
}
