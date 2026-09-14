using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Common;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// The three calls the notification bell makes (BRF-054, RF031–RF035). None of them names a recipient: the
/// API scopes every one of them to the authenticated caller.
/// </summary>
public interface INotificationsApiClient
{
    /// <summary>One page of the caller's inbox, newest first — read and unread alike.</summary>
    Task<Result<PagedResponse<NotificationDto>>> GetNotificationsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>The badge: how many entries the caller has not read yet.</summary>
    Task<Result<UnreadNotificationCountDto>> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks the caller's whole unread inbox as read — the act of opening the bell.</summary>
    Task<Result> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}
