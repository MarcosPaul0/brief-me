using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Notifications.GetMyNotifications;

/// <summary>
/// Returns one page of the authenticated person's in-app inbox, newest first (RF031–RF035, BRF-054) — the
/// read behind the notification bell. The endpoint is <c>GET /notifications</c>.
/// </summary>
/// <remarks>
/// Carries no recipient. The caller <i>is</i> the filter, read from <c>ICurrentUser</c> by the handler, in
/// the same shape as <c>GetTasksByDesignerQuery</c> (BRF-033): a recipient parameter would be an
/// authorization hole one query-string edit wide, over content that includes rejection justifications
/// (RF013/RF032).
/// <para>
/// Read and unread entries alike. The bell shows what happened, not only what is new — the read mark is a
/// property of a row, not a filter on the list.
/// </para>
/// </remarks>
public sealed record GetMyNotificationsQuery(int Page, int PageSize) : IQuery<PagedDto<NotificationDto>>;
