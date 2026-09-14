using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

/// <summary>
/// A unit of design work created for an approved briefing (RF015, RF016). Owns the seven-state machine of
/// RF020: <see cref="UpdateStatus"/> is the only way in or out of a state, and it decides from
/// <see cref="Status"/> alone — never from the other tasks of the briefing (RF057).
/// </summary>
/// <remarks>
/// Named <c>BriefingTask</c> rather than <c>Task</c> to avoid colliding with
/// <see cref="System.Threading.Tasks.Task"/>; the table is still <c>Tasks</c>. Assignment is
/// <see cref="AssignTo"/> (RF017, BRF-027); planning — deadline and priority — is
/// <see cref="SetDeadlineAndPriority"/> (RF018/RF019, BRF-028).
/// </remarks>
public class BriefingTask : AggregateRoot, IAuditableEntity
{
    /// <summary>
    /// The allowed transitions of RF020. Anything absent from this table — including a status to itself —
    /// is rejected. <see cref="BriefingTaskStatus.Approved"/> is terminal here on purpose: the way back out
    /// of it is <see cref="Reopen"/> (RF025, BRF-037), deliberately kept off this table so the transition
    /// this method serves cannot reach it.
    /// </summary>
    private static readonly Dictionary<BriefingTaskStatus, BriefingTaskStatus[]> AllowedTransitions = new()
    {
        [BriefingTaskStatus.NotStarted] =
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.Blocked
        ],
        [BriefingTaskStatus.InProgress] =
        [
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.Blocked
        ],
        [BriefingTaskStatus.AwaitingTechnicalApproval] =
        [
            BriefingTaskStatus.AwaitingRequesterApproval,
            BriefingTaskStatus.Rejected,
            BriefingTaskStatus.Blocked
        ],
        [BriefingTaskStatus.AwaitingRequesterApproval] =
        [
            BriefingTaskStatus.Approved,
            BriefingTaskStatus.Rejected,
            BriefingTaskStatus.Blocked
        ],
        // A rejected delivery goes back to the assigned Designer for correction (RF048).
        [BriefingTaskStatus.Rejected] =
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.Blocked
        ],
        // Unblocking states where the work resumes; the pre-block status is not remembered.
        [BriefingTaskStatus.Blocked] =
        [
            BriefingTaskStatus.NotStarted,
            BriefingTaskStatus.InProgress
        ],
        [BriefingTaskStatus.Approved] = []
    };

    private readonly List<Comment> _comments = [];

    private BriefingTask(
        Guid id,
        Guid briefingId,
        string title,
        Priority priority,
        Guid? designerId,
        DateOnly? deadline) : base(id)
    {
        BriefingId = briefingId;
        Title = title;
        Priority = priority;
        DesignerId = designerId;
        Deadline = deadline;
        Status = BriefingTaskStatus.NotStarted;
    }

    private BriefingTask()
    {
    }

    /// <summary>Briefing this task belongs to (RF016). Exactly one, for the task's whole life.</summary>
    public Guid BriefingId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Assigned Designer (RF017). Null until a Design Manager assigns the task through
    /// <see cref="AssignTo"/> (BRF-027).
    /// </summary>
    public Guid? DesignerId { get; private set; }

    /// <summary>
    /// Delivery deadline (RF018). Null until the Design Manager sets it through
    /// <see cref="SetDeadlineAndPriority"/> (BRF-028) — task creation (BRF-026) never carries one.
    /// </summary>
    public DateOnly? Deadline { get; private set; }

    public Priority Priority { get; private set; }

    public BriefingTaskStatus Status { get; private set; }

    /// <summary>
    /// When the deadline monitor found this task past its due instant (RF028, BRF-043), or <c>null</c> while
    /// it is on time. An instant rather than a flag: it is what the overdue list of RF029 sorts by, what the
    /// notification of RF035 reports, and — being null while unmarked — what makes the monitor's sweep
    /// idempotent without a second column.
    /// </summary>
    /// <remarks>
    /// The mark survives approval on purpose: it is the record that this delivery came out late. What clears
    /// it is replanning (<see cref="SetDeadlineAndPriority"/>), because a new deadline settles the old
    /// breach and puts the task back under watch for the next one.
    /// </remarks>
    public DateTime? OverdueSince { get; private set; }

    /// <summary>Whether the task is currently marked as late. Derived — not a column of its own.</summary>
    public bool IsOverdue => OverdueSince is not null;

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Creates a task for a briefing (RF015, RF016), starting in
    /// <see cref="BriefingTaskStatus.NotStarted"/>. That the briefing is actually approved is checked by the
    /// use case (BRF-026), which is the layer that can load it.
    /// </summary>
    public static BriefingTask Create(
        Guid briefingId,
        string title,
        Priority priority,
        Guid? designerId = null,
        DateOnly? deadline = null)
    {
        if (briefingId == Guid.Empty)
            throw new DomainException("Task briefing is required.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Task title is required.");

        return new BriefingTask(
            Guid.NewGuid(),
            briefingId,
            title.Trim(),
            priority,
            designerId == Guid.Empty ? null : designerId,
            deadline);
    }

    /// <summary>
    /// Whether the task can move from its current <see cref="Status"/> to <paramref name="newStatus"/> under
    /// the RF020 table — the same table <see cref="UpdateStatus"/> applies.
    /// </summary>
    /// <remarks>
    /// Exists so a use case can answer an expected outcome (a stale board, two open tabs) with a conflict
    /// instead of letting the <see cref="DomainException"/> of <see cref="UpdateStatus"/> surface as a 500
    /// (BRF-029, CLAUDE.md — <c>Result</c> over exceptions for expected failures). The table stays private:
    /// the rule has one definition and two uses.
    /// </remarks>
    public bool CanTransitionTo(BriefingTaskStatus newStatus) =>
        Enum.IsDefined(newStatus) && AllowedTransitions[Status].Contains(newStatus);

    /// <summary>
    /// Moves the task to <paramref name="newStatus"/> (RF020), rejecting any transition absent from the
    /// allowed table. Reads only this task's own status, so a task never waits on its siblings (RF057).
    /// </summary>
    /// <remarks>
    /// Raises <see cref="EntityChanged"/>: how far along a task is is the change the people involved follow
    /// most closely (RF034, BRF-052). The event carries no author — the transition is performed by whoever
    /// the use case authorized (the assignee or the Manager, BRF-029) and this method is not told which.
    /// </remarks>
    public void UpdateStatus(BriefingTaskStatus newStatus)
    {
        if (!Enum.IsDefined(newStatus))
            throw new DomainException($"Unknown task status: {(int)newStatus}.");

        if (!CanTransitionTo(newStatus))
            throw new DomainException(
                $"Status transition not allowed: {Status} -> {newStatus}.");

        Status = newStatus;
        RaiseChange(EntityChangeKind.StatusChanged);
    }

    /// <summary>
    /// Reopens an approved task (RF025, BRF-037), sending the work back to
    /// <see cref="BriefingTaskStatus.InProgress"/>. The only way out of
    /// <see cref="BriefingTaskStatus.Approved"/>, which every other write on this aggregate refuses.
    /// </summary>
    /// <remarks>
    /// A method of its own rather than an edge in <see cref="AllowedTransitions"/>: <c>InProgress</c> is one
    /// of the execution states the assignee sets by themselves through <see cref="UpdateStatus"/> (BRF-029),
    /// so an <c>Approved → InProgress</c> edge in that table would let the Designer reopen their own approved
    /// delivery, bypassing the <c>tasks.reopen</c> permission RF025 reserves for the Design Manager.
    /// <para>
    /// Nothing else moves: the assignee (RF017), the deadline and the priority (RF018/RF019) survive the
    /// reopening — handing the task to someone else is reassignment (RF026, BRF-038) and replanning it is
    /// BRF-028, both distinct acts. The parent briefing coming back to execution is the caller's job
    /// (BRF-037): <see cref="Briefing"/> is another aggregate, and this one cannot reach it.
    /// </para>
    /// </remarks>
    public void Reopen()
    {
        if (Status is not BriefingTaskStatus.Approved)
            throw new DomainException(
                $"Reopening not allowed: the task is in {Status}. Only approved tasks can be reopened.");

        Status = BriefingTaskStatus.InProgress;
        RaiseChange(EntityChangeKind.Reopened);
    }

    /// <summary>
    /// Gives the task an assignee (RF017) and raises <see cref="TaskAssigned"/> for the Notifications module
    /// (RF033, BRF-051). Assignment does not move <see cref="Status"/>: having an owner and having started
    /// are different facts, and starting the work is the Designer's act (RF020, BRF-029).
    /// </summary>
    /// <remarks>
    /// Only an <b>unassigned</b> task can be assigned. Handing a task from one Designer to another is
    /// reassignment (RF026, BRF-038), a distinct act with its own permission (<c>tasks.reassign</c>) and its
    /// own notification — including when the "new" assignee is the current one, since a silent success here
    /// would notify the Designer twice for the same task.
    /// </remarks>
    public void AssignTo(Guid designerId, Guid assignedById)
    {
        if (designerId == Guid.Empty)
            throw new DomainException("Task assignee is required.");

        if (assignedById == Guid.Empty)
            throw new DomainException("Task assigner is required.");

        if (DesignerId is not null)
            throw new DomainException(
                "Task is already assigned. Use reassignment to hand it to another designer.");

        // Approved is terminal in RF020: giving an owner to finished work changes nothing. Reopening it is
        // RF025 (BRF-037), which owns that edge.
        if (Status is BriefingTaskStatus.Approved)
            throw new DomainException($"Assignment not allowed: the task is in {Status}.");

        DesignerId = designerId;
        RaiseDomainEvent(new TaskAssigned(Id, BriefingId, designerId, assignedById));
    }

    /// <summary>
    /// Hands an already assigned task to a different Designer (RF026, BRF-038) and raises
    /// <see cref="TaskAssigned"/> for the new owner — the same event the first assignment raises, because
    /// what the assignee needs to know (RF033) does not depend on how the task reached them.
    /// </summary>
    /// <remarks>
    /// The mirror image of <see cref="AssignTo"/>: that one requires no assignee, this one requires there to
    /// be one. Keeping them apart is what lets the two acts carry different permissions
    /// (<c>tasks.assign</c> × <c>tasks.reassign</c>), which is how RF026 reserves the transfer to the Design
    /// Manager.
    /// <para>
    /// Handing the task to whoever already holds it is refused rather than silently accepted: it would raise
    /// <see cref="TaskAssigned"/> again and notify the same Designer twice for the same task. Nothing else
    /// moves — the status (RF020), the deadline and the priority (RF018/RF019) survive the transfer, and the
    /// comments and deliveries keep the authorship of whoever wrote them (RF021, RF052).
    /// </para>
    /// </remarks>
    public void ReassignTo(Guid designerId, Guid reassignedById)
    {
        if (designerId == Guid.Empty)
            throw new DomainException("Task assignee is required.");

        if (reassignedById == Guid.Empty)
            throw new DomainException("Task assigner is required.");

        if (DesignerId is null)
            throw new DomainException(
                "Task is not assigned yet. Use assignment to give it a designer.");

        if (DesignerId == designerId)
            throw new DomainException("Task is already assigned to this designer.");

        // Approved is terminal in RF020: the delivery the requester signed off keeps the designer who made
        // it (RF052). Reopening the task is RF025 (BRF-037), which owns that edge.
        if (Status is BriefingTaskStatus.Approved)
            throw new DomainException($"Reassignment not allowed: the task is in {Status}.");

        DesignerId = designerId;
        RaiseDomainEvent(new TaskAssigned(Id, BriefingId, designerId, reassignedById));

        // Two events, two audiences: TaskAssigned tells the new Designer they have work (RF033), this one
        // tells the requester and the Manager that the work changed hands (RF034, BRF-052). AssignTo raises
        // only the first — giving an owner to a task that had none is not a change to something the involved
        // people were already following.
        RaiseChange(EntityChangeKind.Reassigned, reassignedById);
    }

    /// <summary>
    /// Plans the task: sets its delivery deadline (RF018) and its priority (RF019). Both move together
    /// because planning is a single act of the Design Manager — the use case behind it is BRF-028.
    /// </summary>
    /// <remarks>
    /// Neither the assignee nor the status changes: a deadline says when the work is due, not who does it
    /// (RF017) or how far along it is (RF020). Replanning is allowed in every status but
    /// <see cref="BriefingTaskStatus.Approved"/>, terminal in RF020 — including <c>Blocked</c> and
    /// <c>Rejected</c>, which are exactly when a new date is needed (RF048).
    /// <para>
    /// What <b>does</b> move is <see cref="OverdueSince"/>: a task replanned to a new date is no longer late
    /// against the old one, so the mark is dropped and the task returns to the monitor's sweep (RF028).
    /// </para>
    /// </remarks>
    public void SetDeadlineAndPriority(DateOnly deadline, Priority priority)
    {
        if (!Enum.IsDefined(priority))
            throw new DomainException($"Unknown task priority: {(int)priority}.");

        // A deadline already past on the day it is set would make the task overdue on the SLA monitor's very
        // next pass (RF028). Today counts as future: the deadline is a date, so it runs to the end of the day.
        if (deadline < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("Task deadline cannot be in the past.");

        if (Status is BriefingTaskStatus.Approved)
            throw new DomainException($"Deadline and priority changes are not allowed: the task is in {Status}.");

        Deadline = deadline;
        Priority = priority;

        // A new deadline settles the breach the monitor found against the old one (RF028). Leaving the mark
        // would both misreport the task in the overdue list (RF029) and — since the sweep skips marked
        // tasks — hide the next breach for good.
        OverdueSince = null;

        // When the work is due is what the requester plans around, so replanning is a relevant change
        // (RF034, BRF-052).
        RaiseChange(EntityChangeKind.Replanned);
    }

    /// <summary>
    /// Records that the task is past its due instant (RF028, BRF-043) and raises
    /// <see cref="DeadlineOverdue"/> for the Notifications module (RF035, BRF-053).
    /// <paramref name="dueAtUtc"/> is the instant it was due — the earliest of its own deadline (RF018) and
    /// the applicable SLA rule (RF027) — and <paramref name="detectedAtUtc"/> is when the monitor saw it.
    /// </summary>
    /// <remarks>
    /// Idempotent while already marked, as <c>Briefing.Complete</c> is for its own target: the monitor's
    /// query already excludes marked tasks, and this early return is what keeps "published exactly once" a
    /// property of the aggregate rather than a promise of whoever calls it.
    /// <para>
    /// <b>Which</b> deadline applies is not decided here: an SLA rule is another aggregate, and picking the
    /// one that governs this task means comparing every rule in the table. That is
    /// <see cref="Services.SlaDeadlineResolver"/>, and the caller hands the answer in — the same split
    /// <see cref="Briefing.Complete"/> uses for "are all tasks approved".
    /// </para>
    /// </remarks>
    public void MarkOverdue(DateTime dueAtUtc, DateTime detectedAtUtc)
    {
        if (IsOverdue)
            return;

        // Approved is terminal in RF020: work the requester signed off cannot start being late afterwards.
        if (Status is BriefingTaskStatus.Approved)
            throw new DomainException($"Overdue marking not allowed: the task is in {Status}.");

        OverdueSince = detectedAtUtc;
        RaiseDomainEvent(new DeadlineOverdue(
            DemandSubject.Task,
            Id,
            BriefingId,
            DesignerId,
            dueAtUtc,
            detectedAtUtc));
    }

    /// <summary>
    /// Records a comment on the task (RF021). Allowed in any status: commenting is conversation, not a
    /// state change, and the people involved need it most exactly when a task is blocked or rejected.
    /// </summary>
    public Comment AddComment(Guid authorId, string? text)
    {
        var comment = Comment.Create(Id, authorId, text);
        _comments.Add(comment);
        return comment;
    }

    /// <summary>
    /// Announces a change the people involved in the briefing should hear about (RF034, BRF-052). One place
    /// so every raising site of this aggregate fills the subject and the ids the same way.
    /// </summary>
    private void RaiseChange(EntityChangeKind change, Guid? changedById = null) =>
        RaiseDomainEvent(new EntityChanged(DemandSubject.Task, Id, BriefingId, change, changedById));
}
