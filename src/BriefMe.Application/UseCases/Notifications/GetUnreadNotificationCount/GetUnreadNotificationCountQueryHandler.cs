using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Notifications.GetUnreadNotificationCount;

/// <summary>
/// Counts what the caller has not read yet (RF031–RF035, BRF-054). The cheapest read of the module, and the
/// most frequent one: it is what the bell polls.
/// </summary>
/// <remarks>
/// A count, never the rows. The badge needs the number, and the index <c>(RecipientId, ReadAt)</c> answers
/// it without touching the table (BRF-048, decision #1) — which is what makes a per-minute poll a reasonable
/// alternative to a push (see the slice's design notes).
/// </remarks>
public sealed class GetUnreadNotificationCountQueryHandler(
    INotificationRepository notificationRepository,
    ICurrentUser currentUser) : IQueryHandler<GetUnreadNotificationCountQuery, UnreadNotificationCountDto>
{
    public async Task<Result<UnreadNotificationCountDto>> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var recipientId))
            return Result.Unauthorized<UnreadNotificationCountDto>(CommonErrors.UserNotAuthenticated);

        var count = await notificationRepository.CountUnreadByRecipientAsync(recipientId, cancellationToken);

        return Result.Success(new UnreadNotificationCountDto(count));
    }
}
