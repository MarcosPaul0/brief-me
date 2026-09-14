using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Notifications.GetMyNotifications;
using BriefMe.Application.UseCases.Notifications.GetUnreadNotificationCount;
using BriefMe.Application.UseCases.Notifications.MarkNotificationsAsRead;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

/// <summary>
/// Exposes the in-app inbox over HTTP (BRF-054, RF031–RF035) — the three calls the notification bell makes.
/// A thin controller: request → query/command, dispatch via <see cref="ISender"/>, map the
/// <c>Result</c> to the response.
/// </summary>
/// <remarks>
/// Authenticated but not permission-gated, unlike every other controller here, and deliberately so. A
/// permission answers "may this person act on the system's data"; an inbox is the caller's own rows, and the
/// identity that reads it is the identity that scopes it — each handler takes the recipient from
/// <c>ICurrentUser</c> and none of these routes accepts one. A <c>notifications.view</c> code would have to
/// be granted to every seeded profile and to every profile ever created afterwards, and a gate that must
/// always be open is a hazard rather than a control.
/// <para>
/// The route literal <c>notifications</c> is claimed by no Blazor page — the bell is a component mounted in
/// the layout, not a screen — so there is no <c>AmbiguousMatchException</c> to trip over here.
/// </para>
/// </remarks>
[ApiController]
[Route("notifications")]
[Authorize]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Lists the caller's notifications, newest first (BRF-054), read and unread alike — the bell shows what
    /// happened, not only what is new.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetMyNotificationsQuery(page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar as notificações");
    }

    /// <summary>
    /// How many entries the caller has not read (BRF-054) — the badge. Its own route, because it is polled
    /// on a cadence of its own while the panel stays closed.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUnreadNotificationCountQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao carregar as notificações");
    }

    /// <summary>
    /// Marks the caller's whole unread inbox as read — the act of opening the bell (BRF-054).
    /// </summary>
    /// <remarks>
    /// <c>POST</c> with no body and <c>204</c> in reply: the request carries nothing (the set is "this
    /// person's unread ones") and there is nothing to hand back — the caller that asked for everything to be
    /// read knows the resulting count is zero. Idempotent by the entity's construction, so a repeated call
    /// preserves the first read instant instead of moving it.
    /// </remarks>
    [HttpPost("read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkNotificationsAsReadCommand(), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao marcar as notificações como lidas");
    }
}
