using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Entities;

/// <summary>
/// Design demand request aggregate (RF004–RF014). Born as <see cref="BriefingStatus.Draft"/> and
/// encapsulates the state transitions allowed in this slice of the domain: the requester's lifecycle
/// (RF004–RF010) and the Design Manager's review (RF011–RF014), plus the two execution states driven from
/// outside: <see cref="StartExecution"/> when the briefing is broken down into tasks (RF015, BRF-026) and
/// <see cref="Complete"/> when every one of them is approved (RF024, BRF-036).
/// </summary>
public class Briefing : AggregateRoot, IAuditableEntity
{
    private readonly List<Attachment> _attachments = [];

    private Briefing(
        Guid id,
        string title,
        string? description,
        string? objective,
        string? requestingArea,
        DateOnly? desiredDeliveryDate,
        Priority priority,
        Guid categoryId,
        Guid requesterId) : base(id)
    {
        Title = title;
        Description = description;
        Objective = objective;
        RequestingArea = requestingArea;
        DesiredDeliveryDate = desiredDeliveryDate;
        Priority = priority;
        CategoryId = categoryId;
        RequesterId = requesterId;
        Status = BriefingStatus.Draft;
    }

    private Briefing()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Objective { get; private set; }

    public string? RequestingArea { get; private set; }

    public DateOnly? DesiredDeliveryDate { get; private set; }

    public Priority Priority { get; private set; }

    /// <summary>Briefing category (RF040). Logical FK; the physical constraint arrives with BRF-059.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Requesting user who owns the briefing.</summary>
    public Guid RequesterId { get; private set; }

    public BriefingStatus Status { get; private set; }

    /// <summary>
    /// Reason recorded by the reviewer on the current decision (RF013/RF014). Null while no decision is in
    /// force — including after approval, where no justification applies. The full trail of past decisions is
    /// the History module's job (BRF-057).
    /// </summary>
    public Justification? DecisionJustification { get; private set; }

    /// <summary>Design Manager who took the current decision (RF038).</summary>
    public Guid? ReviewedById { get; private set; }

    public DateTime? ReviewedAt { get; private set; }

    /// <summary>
    /// When the deadline monitor found this briefing past its due instant (RF028, BRF-043), or <c>null</c>
    /// while it is on time. Same shape and same reasons as <see cref="BriefingTask.OverdueSince"/>: an
    /// instant is what RF030's list sorts by, and being null while unmarked is what makes the sweep
    /// idempotent.
    /// </summary>
    /// <remarks>
    /// Kept after <see cref="Complete"/>: it records that the demand was delivered late. Cleared by
    /// <see cref="Edit"/>, which is the one act that can move <see cref="DesiredDeliveryDate"/>.
    /// </remarks>
    public DateTime? OverdueSince { get; private set; }

    /// <summary>Whether the briefing is currently marked as late. Derived — not a column of its own.</summary>
    public bool IsOverdue => OverdueSince is not null;

    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Creates and saves a new briefing as a draft (RF004/RF006). Validation here checks minimal presence
    /// to identify the draft; completeness for submission is verified in <see cref="SubmitForApproval"/>
    /// (BRF-006). Subsequent draft edits go through <see cref="Edit"/>.
    /// </summary>
    public static Briefing Create(
        string title,
        Priority priority,
        Guid categoryId,
        Guid requesterId,
        string? description = null,
        string? objective = null,
        string? requestingArea = null,
        DateOnly? desiredDeliveryDate = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Briefing title is required.");

        if (categoryId == Guid.Empty)
            throw new DomainException("Briefing category is required.");

        if (requesterId == Guid.Empty)
            throw new DomainException("Briefing requester is required.");

        return new Briefing(
            Guid.NewGuid(),
            title.Trim(),
            Normalize(description),
            Normalize(objective),
            Normalize(requestingArea),
            desiredDeliveryDate,
            priority,
            categoryId,
            requesterId);
    }

    /// <summary>
    /// Edits the briefing content (RF009). Allowed in Draft and AwaitingApproval, plus ChangesRequested —
    /// the state a review returns the briefing to precisely so the requester can correct it (RF014, BRF-018).
    /// </summary>
    /// <remarks>
    /// Clears <see cref="OverdueSince"/>: <see cref="DesiredDeliveryDate"/> is one of the fields this method
    /// moves, so an edited briefing is no longer late against the date it used to carry and returns to the
    /// monitor's sweep (RF028). The mark is dropped even when the date itself did not change — the
    /// alternative would be comparing the old value here, and the next sweep re-marks it within the interval
    /// anyway.
    /// <para>
    /// Raises <see cref="EntityChanged"/> only when the briefing has already left <c>Draft</c>: RF034 asks
    /// that the <b>involved</b> people be told, and while the demand is a draft the only person involved is
    /// the one doing the editing. From <c>AwaitingApproval</c> on there is at least a Manager waiting on it.
    /// </para>
    /// </remarks>
    public void Edit(
        string title,
        Priority priority,
        string? description,
        string? objective,
        string? requestingArea,
        DateOnly? desiredDeliveryDate)
    {
        if (Status is not (BriefingStatus.Draft or BriefingStatus.AwaitingApproval or BriefingStatus.ChangesRequested))
            throw new DomainException(
                $"Edit not allowed: the briefing is in {Status}. Editing is only possible in Draft, AwaitingApproval or ChangesRequested.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Briefing title is required.");

        var wasSubmitted = Status is not BriefingStatus.Draft;

        Title = title.Trim();
        Priority = priority;
        Description = Normalize(description);
        Objective = Normalize(objective);
        RequestingArea = Normalize(requestingArea);
        DesiredDeliveryDate = desiredDeliveryDate;
        OverdueSince = null;

        if (wasSubmitted)
            RaiseChange(EntityChangeKind.Edited);
    }

    /// <summary>
    /// Records that the briefing is past its due instant (RF028, BRF-043) and raises
    /// <see cref="DeadlineOverdue"/> for the Notifications module (RF035, BRF-053).
    /// <paramref name="dueAtUtc"/> is the instant it was due — the earliest of
    /// <see cref="DesiredDeliveryDate"/> and the applicable SLA rule (RF027) — and
    /// <paramref name="detectedAtUtc"/> is when the monitor saw it.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="BriefingTask.MarkOverdue"/>, with the same idempotent early return and
    /// the same split of duties: which deadline applies is answered by
    /// <see cref="Services.SlaDeadlineResolver"/> and handed in by the caller.
    /// <para>
    /// Refused in the four statuses the monitor never sweeps: a <see cref="BriefingStatus.Draft"/> was never
    /// submitted, so the sector owes nothing on it, and <see cref="BriefingStatus.Rejected"/>,
    /// <see cref="BriefingStatus.Cancelled"/> and <see cref="BriefingStatus.Completed"/> are closed. A
    /// briefing still <see cref="BriefingStatus.AwaitingApproval"/> <b>is</b> marked: sitting unreviewed past
    /// the requested date is exactly the delay RF030 exists to surface.
    /// </para>
    /// </remarks>
    public void MarkOverdue(DateTime dueAtUtc, DateTime detectedAtUtc)
    {
        if (IsOverdue)
            return;

        if (Status is BriefingStatus.Draft
            or BriefingStatus.Rejected
            or BriefingStatus.Cancelled
            or BriefingStatus.Completed)
            throw new DomainException($"Overdue marking not allowed: the briefing is in {Status}.");

        OverdueSince = detectedAtUtc;

        // SubjectId and BriefingId are the same id here: the late demand is the briefing itself. The event
        // carries both so a consumer never has to branch on Subject just to name the briefing.
        RaiseDomainEvent(new DeadlineOverdue(
            DemandSubject.Briefing,
            Id,
            Id,
            RequesterId,
            dueAtUtc,
            detectedAtUtc));
    }

    /// <summary>
    /// Submits the briefing for approval (RF007). Requires the mandatory RF005 fields; returns a validation
    /// failure (without changing state) when one is missing. Category checklist validation (RF063) is
    /// deferred to BRF-093.
    /// </summary>
    /// <remarks>
    /// Attachments are deliberately <b>not</b> required: BRF-001 models them as "zero or more", and RF005
    /// lists them among the briefing's fields, not among the conditions for review — a demand can be fully
    /// described in prose. The rule that used to be here also could never hold: callers load the aggregate
    /// through <c>GetByIdAsync</c>, which does not include the collection, so the count read zero even for a
    /// briefing whose files were in the database.
    /// </remarks>
    public Result SubmitForApproval()
    {
        if (Status is not (BriefingStatus.Draft or BriefingStatus.ChangesRequested))
            throw new DomainException(
                $"Submission for approval not allowed: the briefing is in {Status}.");

        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(Description))
            errors.Add(new ValidationError(nameof(Description), "Description is required."));

        if (string.IsNullOrWhiteSpace(Objective))
            errors.Add(new ValidationError(nameof(Objective), "Objective is required."));

        if (string.IsNullOrWhiteSpace(RequestingArea))
            errors.Add(new ValidationError(nameof(RequestingArea), "Requesting area is required."));

        if (DesiredDeliveryDate is null)
            errors.Add(new ValidationError(nameof(DesiredDeliveryDate), "Desired delivery date is required."));

        if (errors.Count > 0)
            return Result.Failure(errors);

        Status = BriefingStatus.AwaitingApproval;
        ClearDecision();
        RaiseDomainEvent(new BriefingSubmittedForApproval(Id, RequesterId));
        return Result.Success();
    }

    /// <summary>Approves the briefing (RF011). Only from <see cref="BriefingStatus.AwaitingApproval"/>.</summary>
    public void Approve(Guid approvedById)
    {
        EnsureUnderReview("Approval");
        EnsureReviewer(approvedById);

        Status = BriefingStatus.Approved;
        RegisterDecision(approvedById, justification: null);
        RaiseDomainEvent(new BriefingApproved(Id, RequesterId, approvedById));
    }

    /// <summary>
    /// Rejects the briefing (RF012). The justification is mandatory (RF013): an absent, blank or oversized
    /// one throws <see cref="DomainException"/> and leaves the status untouched.
    /// </summary>
    public void Reject(string? justification, Guid rejectedById)
    {
        EnsureUnderReview("Rejection");
        EnsureReviewer(rejectedById);

        var reason = ParseJustification(justification);

        Status = BriefingStatus.Rejected;
        RegisterDecision(rejectedById, reason);
        RaiseDomainEvent(new BriefingRejected(Id, RequesterId, rejectedById, reason.Value));
    }

    /// <summary>
    /// Returns the briefing to the requester for corrections (RF014), keeping it editable
    /// (<see cref="BriefingStatus.ChangesRequested"/>). Justification is mandatory, as in
    /// <see cref="Reject"/> — the requester needs to know what to fix.
    /// </summary>
    public void RequestChanges(string? justification, Guid requestedById)
    {
        EnsureUnderReview("Change request");
        EnsureReviewer(requestedById);

        var reason = ParseJustification(justification);

        Status = BriefingStatus.ChangesRequested;
        RegisterDecision(requestedById, reason);
        RaiseDomainEvent(new ChangesRequested(Id, RequesterId, requestedById, reason.Value));
    }

    /// <summary>
    /// Puts the briefing under execution once it is broken down into tasks (RF015, BRF-026). Idempotent
    /// while already <see cref="BriefingStatus.InProgress"/>: RF015 does not require the whole decomposition
    /// to happen at once, so a second batch of tasks must not fail. From here on the briefing can no longer
    /// be cancelled (RF010) — execution has started.
    /// </summary>
    public void StartExecution()
    {
        if (Status is BriefingStatus.InProgress)
            return;

        if (Status is not BriefingStatus.Approved)
            throw new DomainException(
                $"Execution cannot start: the briefing is in {Status}. Only approved briefings can be broken down into tasks.");

        Status = BriefingStatus.InProgress;
    }

    /// <summary>
    /// Closes the briefing once every one of its tasks has been approved (RF024, BRF-036). The only way into
    /// <see cref="BriefingStatus.Completed"/>: <see cref="Status"/> has a private setter precisely so RF058 —
    /// completion requires all tasks approved — stays an invariant of this aggregate rather than a convention
    /// each caller is trusted to keep.
    /// </summary>
    /// <remarks>
    /// Whether all tasks are approved is <b>not</b> decided here: <see cref="BriefingTask"/> is an aggregate
    /// of its own, so this one cannot count them. The caller (BRF-036) loads them and asks
    /// <see cref="Services.BriefingProgressCalculator"/> — the same split
    /// <see cref="AddDelivery"/> uses for a task's status.
    /// <para>
    /// Idempotent while already <see cref="BriefingStatus.Completed"/>, as
    /// <see cref="StartExecution"/> is for InProgress: the completion rule reacts to a domain event, and an
    /// at-least-once dispatch (BRF-049) must not turn a redelivery into an error. Any other status throws —
    /// a briefing that never entered execution has no delivery to complete.
    /// </para>
    /// </remarks>
    public void Complete()
    {
        if (Status is BriefingStatus.Completed)
            return;

        if (Status is not BriefingStatus.InProgress)
            throw new DomainException(
                $"Completion not allowed: the briefing is in {Status}. Only a briefing in execution can be completed.");

        Status = BriefingStatus.Completed;

        // After the early return, never before it: a redelivered event must not produce a second "your
        // briefing is done" for a briefing that was already done.
        RaiseChange(EntityChangeKind.Completed);
    }

    /// <summary>
    /// Brings a completed briefing back into execution when one of its tasks is reopened (RF025, BRF-037).
    /// The only way out of <see cref="BriefingStatus.Completed"/>, and the counterpart of
    /// <see cref="Complete"/>.
    /// </summary>
    /// <remarks>
    /// Reopening a task is what makes RF058 false again — a briefing may only be complete while every task is
    /// approved — so this transition is a consequence of that act, never one taken on its own. The caller
    /// (BRF-037) reopens the task and the briefing in the same transaction, so no instant exists with a
    /// reopened task under a completed briefing.
    /// <para>
    /// Idempotent while already <see cref="BriefingStatus.InProgress"/>, as <see cref="StartExecution"/> and
    /// <see cref="Complete"/> are for their own targets: reopening the second approved task of a briefing that
    /// still had pending work is ordinary, not an error. Any other status throws — a briefing that never
    /// reached execution has no approved task to reopen.
    /// </para>
    /// </remarks>
    public void Reopen()
    {
        if (Status is BriefingStatus.InProgress)
            return;

        if (Status is not BriefingStatus.Completed)
            throw new DomainException(
                $"Reopening not allowed: the briefing is in {Status}. Only a completed briefing can return to execution.");

        Status = BriefingStatus.InProgress;
        RaiseChange(EntityChangeKind.Reopened);
    }

    /// <summary>
    /// Cancels the briefing (RF010). Not allowed after execution has started. Announces itself to the people
    /// involved (RF034) unless the briefing was still a draft — a demand nobody but its requester ever saw.
    /// </summary>
    public void Cancel()
    {
        if (Status is BriefingStatus.InProgress or BriefingStatus.Completed)
            throw new DomainException(
                $"Cancellation not allowed: the briefing is in {Status}.");

        if (Status is BriefingStatus.Cancelled)
            throw new DomainException("The briefing is already cancelled.");

        var wasSubmitted = Status is not BriefingStatus.Draft;

        Status = BriefingStatus.Cancelled;

        if (wasSubmitted)
            RaiseChange(EntityChangeKind.Cancelled);
    }

    public Attachment AddAttachment(string fileName, string storagePath, long sizeBytes, Guid uploadedById)
    {
        if (Status is not (BriefingStatus.Draft or BriefingStatus.AwaitingApproval or BriefingStatus.ChangesRequested))
            throw new DomainException(
                $"Cannot attach files while the briefing is in {Status}.");

        var attachment = Attachment.Create(Id, fileName, storagePath, sizeBytes, uploadedById);
        _attachments.Add(attachment);
        return attachment;
    }

    /// <summary>
    /// Attaches a file produced while executing one of the briefing's tasks (RF022, BRF-031), recording its
    /// author and moment (RF052). The RF022 counterpart of <see cref="AddAttachment"/>: both create an
    /// <see cref="Attachment"/> on this aggregate, but a delivery carries the task it answers and is accepted
    /// under the opposite status rule.
    /// </summary>
    /// <remarks>
    /// Only <see cref="BriefingStatus.InProgress"/> accepts a delivery: a task exists only after the briefing
    /// is broken down (<see cref="StartExecution"/>, RF015), <see cref="BriefingStatus.Completed"/> means
    /// every task is already approved — reopening one is RF025 (BRF-037), which brings the briefing back to
    /// InProgress first — and the earlier states have no tasks at all.
    /// <para>
    /// Whether the <b>task</b> accepts the delivery is not decided here: this aggregate cannot read a task's
    /// status without reaching across a boundary. The use case (BRF-031) answers that as an expected
    /// <c>Result</c>, the same split <c>BriefingTask.CanTransitionTo</c> uses.
    /// </para>
    /// </remarks>
    public Attachment AddDelivery(Guid taskId, string fileName, string storagePath, long sizeBytes, Guid uploadedById)
    {
        if (Status is not BriefingStatus.InProgress)
            throw new DomainException(
                $"Cannot attach a task delivery while the briefing is in {Status}. Deliveries are only accepted while the briefing is in execution.");

        var delivery = Attachment.CreateDelivery(Id, taskId, fileName, storagePath, sizeBytes, uploadedById);
        _attachments.Add(delivery);
        return delivery;
    }

    public void RemoveAttachment(Guid attachmentId)
    {
        if (Status is not (BriefingStatus.Draft or BriefingStatus.AwaitingApproval or BriefingStatus.ChangesRequested))
            throw new DomainException(
                $"Cannot remove attachments while the briefing is in {Status}.");

        _attachments.RemoveAll(a => a.Id == attachmentId);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Justification ParseJustification(string? value)
    {
        var result = Justification.Create(value);

        return result.IsSuccess
            ? result.Value
            : throw new DomainException(result.Error ?? "Justification is required.");
    }

    private void EnsureUnderReview(string operation)
    {
        if (Status is not BriefingStatus.AwaitingApproval)
            throw new DomainException(
                $"{operation} not allowed: the briefing is in {Status}. Only briefings in AwaitingApproval can be reviewed.");
    }

    private static void EnsureReviewer(Guid reviewerId)
    {
        if (reviewerId == Guid.Empty)
            throw new DomainException("The reviewing manager is required.");
    }

    private void RegisterDecision(Guid reviewerId, Justification? justification)
    {
        DecisionJustification = justification;
        ReviewedById = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Drops the previous decision when a new review cycle starts, so a briefing resubmitted after
    /// <see cref="RequestChanges"/> does not carry the old reason as if it were current.
    /// </summary>
    private void ClearDecision()
    {
        DecisionJustification = null;
        ReviewedById = null;
        ReviewedAt = null;
    }

    /// <summary>
    /// Announces a change the people involved should hear about (RF034, BRF-052). Not raised by the review
    /// transitions — <see cref="Approve"/>, <see cref="Reject"/>, <see cref="RequestChanges"/> and
    /// <see cref="SubmitForApproval"/> have events of their own (BRF-015), and a second one here would
    /// notify the same person twice for the same decision.
    /// </summary>
    private void RaiseChange(EntityChangeKind change, Guid? changedById = null) =>
        RaiseDomainEvent(new EntityChanged(DemandSubject.Briefing, Id, Id, change, changedById));
}
