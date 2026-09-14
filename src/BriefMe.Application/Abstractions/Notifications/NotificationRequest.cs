using BriefMe.Domain.Enums;

namespace BriefMe.Application.Abstractions.Notifications;

/// <summary>
/// One thing to tell one person (RF031–RF035, BRF-047): the input of
/// <see cref="INotificationService"/>. Inbound data, so it is not an output DTO and does not live in
/// <c>Common/Dtos</c>; it belongs next to the contract that consumes it, like the commands do.
/// </summary>
/// <remarks>
/// Carries no channel and no recipient address. Which channels a request fans out to is the delivery
/// concern the interface exists to hide — adding e-mail later reads the recipient's address from the
/// <c>User</c> aggregate and changes nothing here.
/// <para>
/// The text is already written by the caller. Composing "Briefing X was approved" needs the event's data and
/// the wording of the feature that raised it, both of which the handler (BRF-050–053) holds and the delivery
/// service does not.
/// </para>
/// </remarks>
/// <param name="RecipientId">Who to tell. One request per person — RF034 notifies several, as several requests.</param>
/// <param name="EventType">Why they are being told (RF031–RF035).</param>
/// <param name="Title">One-line summary; at most <see cref="Domain.Entities.Notification.MaxTitleLength"/> characters.</param>
/// <param name="Message">The full sentence; at most <see cref="Domain.Entities.Notification.MaxMessageLength"/> characters.</param>
public sealed record NotificationRequest(
    Guid RecipientId,
    NotificationEventType EventType,
    string Title,
    string Message);
