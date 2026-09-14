using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Failure messages for the task use cases (RF015, RF016). Messages about the parent briefing itself
/// (not found, unauthenticated) come from <see cref="BriefingErrors"/> / <see cref="CommonErrors"/>.
/// </summary>
public static class BriefingTaskErrors
{
    public const string NotFound = "Task not found.";

    /// <summary>Assignment target (RF017): the user id does not resolve to a live user.</summary>
    public const string DesignerNotFound = "Designer not found.";

    /// <summary>RF017 assigns to a Designer — holding the role is what makes someone one.</summary>
    public const string DesignerRoleRequired = "The selected user is not a Designer.";

    public const string DesignerInactive = "The selected designer is inactive.";

    /// <summary>The Designer role itself is missing from the database (USR-047 seed not applied).</summary>
    public const string DesignerRoleNotConfigured = "The Designer role is not configured in the system.";

    /// <summary>Assignment is for unassigned tasks only; handing a task over is RF026 (BRF-038).</summary>
    public const string AlreadyAssigned =
        "Task is already assigned. Use reassignment to hand it to another designer.";

    /// <summary>The mirror of <see cref="AlreadyAssigned"/>: reassignment (RF026) needs someone to replace.</summary>
    public const string NotAssigned =
        "Task is not assigned yet. Use assignment to give it a designer.";

    /// <summary>
    /// RF026 transfers a task to <b>another</b> Designer. Accepting the current one silently would raise
    /// <c>TaskAssigned</c> again and notify the same person twice for the same task (RF033).
    /// </summary>
    public const string AlreadyAssignedToDesigner = "Task is already assigned to this designer.";

    public static string CreationNotAllowed(BriefingStatus status) =>
        $"Task creation not allowed: the briefing is in {status}. Only approved briefings can be broken down into tasks.";

    public static string AssignmentNotAllowed(BriefingTaskStatus status) =>
        $"Assignment not allowed: the task is in {status}. Only tasks that are not yet approved can be assigned.";

    /// <summary>
    /// Transferring an approved task (RF026) — the delivery the requester signed off keeps the designer who
    /// made it (RF052). Reopening it first is RF025 (BRF-037).
    /// </summary>
    public static string ReassignmentNotAllowed(BriefingTaskStatus status) =>
        $"Reassignment not allowed: the task is in {status}. Only tasks that are not yet approved can be reassigned.";

    /// <summary>Replanning an approved task (RF018/RF019) — reopening it is RF025 (BRF-037).</summary>
    public static string DeadlineAndPriorityChangeNotAllowed(BriefingTaskStatus status) =>
        $"Deadline and priority changes are not allowed: the task is in {status}. Only tasks that are not yet approved can be replanned.";

    /// <summary>RF020 is the assignee's declaration about their own work; a Design Manager may also drive it.</summary>
    public const string NotAssignedToCaller =
        "Only the assigned designer or a design manager can update the status of this task.";

    /// <summary>
    /// The requested status belongs to the delivery approval flow (RF042–RF048) and is reached through its own
    /// use cases, which carry the rules this command cannot apply (attached delivery, justification, approver).
    /// </summary>
    public static string StatusNotSetDirectly(BriefingTaskStatus status) =>
        $"Status {status} is not set directly: it is the outcome of the delivery approval flow. Use the corresponding approval action.";

    /// <summary>Mirrors the RF020 transition table of <c>BriefingTask</c> as an expected failure (BRF-029).</summary>
    public static string StatusTransitionNotAllowed(BriefingTaskStatus from, BriefingTaskStatus to) =>
        $"Status transition not allowed: {from} -> {to}.";

    /// <summary>
    /// RF021 gives commenting to "the involved": the assigned designer, the requester of the parent briefing
    /// and the design management that drives the pipeline (BRF-030).
    /// </summary>
    public const string NotInvolvedInTask = "Only the people involved in the task can comment on it.";

    /// <summary>
    /// Reading the record of a task — its comment trail and its deliveries (BRF-034) — is open to the same
    /// set RF021 lets speak on it: whoever may join the conversation may read it.
    /// </summary>
    public const string NotInvolvedToView = "Only the people involved in the task can view it.";

    /// <summary>
    /// RF022 names the designer as the one who delivers; design management may drive any task (BRF-029).
    /// Unlike commenting (RF021, BRF-030) the requester is not included — their part in a delivery is
    /// approving it (RF046).
    /// </summary>
    public const string NotAssignedToDeliver =
        "Only the assigned designer or a design manager can submit a delivery for this task.";

    /// <summary>
    /// Approved is terminal in RF020, so the work it closed takes no further file. Reopening the task is
    /// RF025 (BRF-037).
    /// </summary>
    public static string DeliveryNotAllowed(BriefingTaskStatus status) =>
        $"Delivery not allowed: the task is in {status}. Only tasks that are not yet approved can receive a delivery.";

    /// <summary>
    /// RF025 reopens <b>completed</b> work (BRF-037). A task already under execution has nothing to reopen —
    /// moving it around is RF020 (BRF-029).
    /// </summary>
    public static string ReopenNotAllowed(BriefingTaskStatus status) =>
        $"Reopening not allowed: the task is in {status}. Only approved tasks can be reopened.";

    public static class Validation
    {
        public const string AtLeastOneTaskRequired = "At least one task is required.";

        public const string TitleRequired = "Task title is required.";

        public const string InvalidPriority = "Invalid task priority.";

        public const string TaskRequired = "Task is required.";

        public const string DesignerRequired = "Designer is required.";

        public const string DeadlineRequired = "Task deadline is required.";

        /// <summary>RF018: a deadline set already past would be overdue on the SLA monitor's next pass.</summary>
        public const string DeadlineInPast = "Task deadline cannot be in the past.";

        public const string InvalidStatus = "Invalid task status.";

        /// <summary>RF021: a comment with no text is not a comment (BRF-030).</summary>
        public const string CommentTextRequired = "Comment text is required.";

        /// <summary>RF022: a delivery with no file is not a delivery (BRF-031).</summary>
        public const string FileNameRequired = "File name is required.";

        public const string FileContentRequired = "File content is required.";

        /// <summary>
        /// Presence and shape only. The configured limit on how big a file may be belongs to
        /// <c>IFileValidator</c> (BRF-011), which reads it from Infrastructure settings.
        /// </summary>
        public const string FileSizeMustBePositive = "File size must be greater than zero.";

        public static string TitleTooLong(int maxLength) => $"Task title must be at most {maxLength} characters.";

        public static string FileNameTooLong(int maxLength) =>
            $"File name must be at most {maxLength} characters.";

        public static string CommentTextTooLong(int maxLength) =>
            $"Comment text must be at most {maxLength} characters.";
    }
}
