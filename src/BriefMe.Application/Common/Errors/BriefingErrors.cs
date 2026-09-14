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

    /// <summary>
    /// Involvement guard for the tracking read (BRF-039, RF023). Wider than <see cref="NotOwnerToView"/> on
    /// purpose: design management runs the pipeline it reports on — it approves the briefing (RF011) and
    /// creates its tasks (RF015) — so the progress of someone else's briefing is legitimately theirs to read.
    /// </summary>
    public const string NotInvolvedToTrack =
        "Only the requester who owns the briefing or design management can track it.";

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

    public static string ApprovalNotAllowed(BriefingStatus status) =>
        $"Approval not allowed: the briefing is in {status}. Only briefings in AwaitingApproval can be reviewed.";

    public static string RejectionNotAllowed(BriefingStatus status) =>
        $"Rejection not allowed: the briefing is in {status}. Only briefings in AwaitingApproval can be reviewed.";

    public static string ChangeRequestNotAllowed(BriefingStatus status) =>
        $"Change request not allowed: the briefing is in {status}. Only briefings in AwaitingApproval can be reviewed.";

    public static string CancellationNotAllowed(BriefingStatus status) =>
        $"Cancellation not allowed: the briefing is in {status}.";

    /// <summary>
    /// A task delivery (RF022, BRF-031) hangs off the briefing aggregate, and only a briefing under
    /// execution has tasks being worked on. Owned here because the blocking fact is the briefing's status.
    /// </summary>
    public static string DeliveryNotAllowed(BriefingStatus status) =>
        $"Delivery not allowed: the briefing is in {status}. Deliveries are only accepted while the briefing is in execution.";

    /// <summary>
    /// Reopening a task (RF025, BRF-037) brings its briefing back to execution. Owned here because the
    /// blocking fact is the briefing's status: only a completed one — or one already in execution — has an
    /// approved task to reopen.
    /// </summary>
    public static string ReopenNotAllowed(BriefingStatus status) =>
        $"Reopening not allowed: the briefing is in {status}. Only a completed briefing can return to execution.";

    /// <summary>
    /// The attachment does not hang off this briefing (RF005). Also the answer for an id that names a task
    /// delivery: those belong to the task screen (BRF-034) and are invisible to the briefing's own read.
    /// </summary>
    public const string AttachmentNotFound = "Attachment not found on this briefing.";

    public static string AttachmentNotAllowed(BriefingStatus status) =>
        $"Attaching files is not allowed: the briefing is in {status}. Files can only be attached in Draft, AwaitingApproval or ChangesRequested.";

    public static string AttachmentRemovalNotAllowed(BriefingStatus status) =>
        $"Removing attachments is not allowed: the briefing is in {status}. Files can only be removed in Draft, AwaitingApproval or ChangesRequested.";

    public static class Validation
    {
        public const string BriefingRequired = "Briefing is required.";

        public const string TitleRequired = "Title is required.";

        public const string InvalidPriority = "Invalid priority.";

        public const string InvalidStatus = "Invalid status.";

        public const string AttachmentRequired = "Attachment is required.";

        /// <summary>
        /// RF005: an upload with no file is not an attachment. Restated here rather than shared with
        /// <c>BriefingTaskErrors</c> — the owning concept is the briefing's own material, not a task
        /// delivery, and a catalog message only moves to <c>CommonErrors</c> once two features need the very
        /// same string in the very same role.
        /// </summary>
        public const string FileNameRequired = "File name is required.";

        public const string FileContentRequired = "File content is required.";

        /// <summary>
        /// Presence and shape only. How big a file may be is <c>IFileValidator</c>'s configured rule
        /// (BRF-011), read from Infrastructure settings the Application layer must not know about.
        /// </summary>
        public const string FileSizeMustBePositive = "File size must be greater than zero.";

        public static string FileNameTooLong(int maxLength) =>
            $"File name must be at most {maxLength} characters.";
    }
}
