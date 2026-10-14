using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Single source of truth for the failure messages returned by the briefing use cases (RF005–RF014).
/// Handler messages sit at the top level; <see cref="Validation"/> holds the FluentValidation wording.
/// </summary>
public static class BriefingErrors
{
    public const string NotFound = "Briefing not found.";

    public const string NotOwnerToView = "Only the requester who owns the briefing can view it.";

    public const string NotOwnerToEdit = "Only the requester who owns the briefing can edit it.";

    /// <summary>
    /// Ownership guard for submission (BRF-007). Also reused by cancellation (BRF-011), whose wording
    /// still reads "submit it for approval" — kept verbatim so the API contract does not change.
    /// </summary>
    public const string NotOwnerToSubmit = "Only the requester who owns the briefing can submit it for approval.";

    public const string EditNotAllowed =
        "Edit not allowed: the briefing can only be edited in Draft, AwaitingApproval or ChangesRequested.";

    public const string NotSubmittable = "The briefing can only be submitted from Draft or ChangesRequested.";

    public const string AlreadyCancelled = "The briefing is already cancelled.";

    public static string CancellationNotAllowed(BriefingStatus status) =>
        $"Cancellation not allowed: the briefing is in {status}.";

    public static class Validation
    {
        public const string BriefingRequired = "Briefing is required.";

        public const string TitleRequired = "Title is required.";

        public const string InvalidPriority = "Invalid priority.";

        public const string InvalidStatus = "Invalid status.";
    }
}
