using BriefMe.Application.Abstractions.Messaging;

namespace BriefMe.Application.UseCases.Notifications.MarkNotificationsAsRead;

/// <summary>
/// Marks every unread entry of the authenticated person's inbox as read (BRF-054) — what opening the bell
/// does. The endpoint is <c>POST /notifications/read</c>.
/// </summary>
/// <remarks>
/// Bulk and not per row, because that is the gesture the acceptance criterion describes: "given unread
/// notifications, when the user opens the bell, then they all get marked as read". Marking only the rendered
/// page would leave a badge counting entries over a panel the reader has just emptied.
/// <para>
/// Carries nothing — not even the ids the panel rendered. The set to mark is "this person's unread ones as
/// they stand now", so a notification that arrived between the list load and this call is marked too, and
/// the caller cannot mark someone else's.
/// </para>
/// <para>
/// Returns no value: the client that asked for everything to be read already knows the resulting count is
/// zero. Value-less <c>ICommand</c>, as <c>DeleteUserCommand</c> and <c>LogoutCommand</c> are.
/// </para>
/// </remarks>
public sealed record MarkNotificationsAsReadCommand : ICommand;
