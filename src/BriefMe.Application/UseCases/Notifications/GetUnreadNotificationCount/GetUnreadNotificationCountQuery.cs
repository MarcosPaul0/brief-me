using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Notifications.GetUnreadNotificationCount;

/// <summary>
/// Returns how many entries of the authenticated person's inbox are still unread (RF031–RF035, BRF-054) —
/// the bell's badge. The endpoint is <c>GET /notifications/unread-count</c>.
/// </summary>
/// <remarks>
/// A route of its own rather than the <c>TotalCount</c> of the listing, unlike the SLA dashboard where the
/// number and the list are one read (BRF-046): here the badge is polled every minute while the panel is
/// closed and the list is fetched only when it opens, so sharing the read would mean carrying ten rows a
/// minute to display one integer.
/// <para>
/// Parameterless, like the query it answers: which inbox is the caller's own, and there is nothing to
/// filter — which is also why it has no validator.
/// </para>
/// </remarks>
public sealed record GetUnreadNotificationCountQuery : IQuery<UnreadNotificationCountDto>;
