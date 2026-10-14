using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Single source of truth for the failure messages returned by the briefing use cases.
/// Handler messages sit at the top level; <see cref="Validation"/> holds the FluentValidation wording.
/// </summary>
public static class BriefingErrors
{
    public const string NotFound = "Briefing not found.";

    /// <summary>
    /// A requester opening someone else's briefing, or a designer opening one that is neither in the queue
    /// nor assigned to them.
    /// </summary>
    public const string NotVisible = "You do not have access to this briefing.";

    public const string NotOwnerToEdit = "Only the requester who owns the briefing can edit it.";

    /// <summary>Ownership guard shared by submit and cancel.</summary>
    public const string NotOwnerToSubmit = "Only the requester who owns the briefing can submit or cancel it.";

    public const string NotOwnerToReview = "Only the requester who owns the briefing can accept or send back its delivery.";

    public const string NotAssignee = "Only the designer who claimed the briefing can deliver it.";

    public const string EditNotAllowed = "Edit not allowed: the briefing can only be edited in Draft or Submitted.";

    public const string NotSubmittable = "The briefing can only be submitted from Draft.";

    public const string AlreadyCancelled = "The briefing is already cancelled.";

    /// <summary>Two designers claimed at once and the other one committed first.</summary>
    public const string AlreadyClaimed = "This briefing was already claimed by another designer.";

    public static string CancellationNotAllowed(BriefingStatus status) =>
        $"Cancellation not allowed: the briefing is in {status}.";

    public static string ClaimNotAllowed(BriefingStatus status) =>
        $"Claim not allowed: the briefing is in {status}. Only submitted briefings can be claimed.";

    public static string DeliveryNotAllowed(BriefingStatus status) =>
        $"Delivery not allowed: the briefing is in {status}. Only briefings in progress can be delivered.";

    public static string AcceptNotAllowed(BriefingStatus status) =>
        $"Acceptance not allowed: the briefing is in {status}. Only delivered briefings can be accepted.";

    public static string ChangeRequestNotAllowed(BriefingStatus status) =>
        $"Change request not allowed: the briefing is in {status}. Only delivered briefings can be sent back.";

    public static class Validation
    {
        public const string BriefingRequired = "Briefing is required.";

        public const string TitleRequired = "Title is required.";

        public const string InvalidPriority = "Invalid priority.";

        public const string InvalidStatus = "Invalid status.";

        public const string DeliveryNoteRequired = "Delivery note is required.";

        public static string DeliveryNoteTooLong(int maxLength) =>
            $"Delivery note must be at most {maxLength} characters.";
    }
}
