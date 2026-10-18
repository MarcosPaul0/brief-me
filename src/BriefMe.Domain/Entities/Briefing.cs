using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Entities;

/// <summary>
/// A design request and the work done on it. The requester drafts and submits it; it then waits in the open
/// queue until a designer claims it; the designer delivers; the requester accepts or asks for changes, which
/// sends it back to the same designer.
/// </summary>
/// <remarks>
/// Every transition guards its own source status and throws <see cref="DomainException"/> otherwise. Use
/// cases check the status first and answer an expected conflict, so an exception here means a caller skipped
/// that check. Who may act (owner, assignee) is the use case's job; the aggregate only checks that a designer
/// delivering is the one who claimed it, since that is a fact it holds.
/// </remarks>
public class Briefing : AggregateRoot, IAuditableEntity
{
    public const int DeliveryNoteMaxLength = 4000;

    /// <summary>Most attachments one briefing may hold (ATT-04).</summary>
    public const int MaxAttachments = 10;

    private readonly List<BriefingAttachment> _attachments = [];

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

    public Guid CategoryId { get; private set; }

    /// <summary>The user who wrote the briefing and decides whether the delivery is accepted.</summary>
    public Guid RequesterId { get; private set; }

    /// <summary>The designer who claimed the briefing; null while it is a draft or in the queue.</summary>
    public Guid? DesignerId { get; private set; }

    public BriefingStatus Status { get; private set; }

    /// <summary>What the designer handed over (a link, a summary). Replaced on every delivery.</summary>
    public string? DeliveryNote { get; private set; }

    public DateTime? DeliveredAt { get; private set; }

    /// <summary>
    /// What the requester asked to change on the last delivery. Set by <see cref="RequestChanges"/> and
    /// cleared by the next <see cref="Deliver"/>, so it is only present while the designer still owes it.
    /// </summary>
    public Justification? ChangeRequestReason { get; private set; }

    /// <summary>Reference files from the requester. Only loaded when the repository is asked for them.</summary>
    public IReadOnlyCollection<BriefingAttachment> Attachments => _attachments.AsReadOnly();

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Creates a draft. Only the title, priority, category and requester are needed here; the rest is
    /// required on <see cref="Submit"/>.
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

    /// <summary>True while nobody is working on the briefing: the requester can still edit or cancel it.</summary>
    public bool IsOpenForRequester => Status is BriefingStatus.Draft or BriefingStatus.Submitted;

    /// <summary>Edits the content. Allowed in Draft and Submitted — once claimed, the designer works on what was sent.</summary>
    public void Edit(
        string title,
        Priority priority,
        string? description,
        string? objective,
        string? requestingArea,
        DateOnly? desiredDeliveryDate)
    {
        if (!IsOpenForRequester)
            throw new DomainException(
                $"Edit not allowed: the briefing is in {Status}. Editing is only possible in Draft or Submitted.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Briefing title is required.");

        Title = title.Trim();
        Priority = priority;
        Description = Normalize(description);
        Objective = Normalize(objective);
        RequestingArea = Normalize(requestingArea);
        DesiredDeliveryDate = desiredDeliveryDate;
    }

    /// <summary>
    /// Records a file already written to storage under <paramref name="storageKey"/>. Same window as
    /// <see cref="Edit"/>: once a designer claims the briefing, its material is fixed.
    /// </summary>
    public BriefingAttachment AddAttachment(string? fileName, string storageKey, AttachmentFormat format, long sizeBytes)
    {
        if (!IsOpenForRequester)
            throw new DomainException(
                $"Attachment not allowed: the briefing is in {Status}. Attachments can only change in Draft or Submitted.");

        if (_attachments.Count >= MaxAttachments)
            throw new DomainException($"A briefing can have at most {MaxAttachments} attachments.");

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("Attachment storage key is required.");

        ArgumentNullException.ThrowIfNull(format);

        if (sizeBytes is <= 0 or > AttachmentFormat.MaxSizeBytes)
            throw new DomainException($"Attachment size must be between 1 byte and {AttachmentFormat.MaxSizeBytes} bytes.");

        var attachment = BriefingAttachment.Create(Id, fileName, storageKey, format, sizeBytes);
        _attachments.Add(attachment);
        return attachment;
    }

    /// <summary>
    /// Drops one attachment and returns it, so the caller can delete its object once the change is committed.
    /// </summary>
    public BriefingAttachment RemoveAttachment(Guid attachmentId)
    {
        if (!IsOpenForRequester)
            throw new DomainException(
                $"Attachment not allowed: the briefing is in {Status}. Attachments can only change in Draft or Submitted.");

        var attachment = _attachments.Find(a => a.Id == attachmentId)
            ?? throw new DomainException("Attachment not found in this briefing.");

        _attachments.Remove(attachment);
        return attachment;
    }

    /// <summary>
    /// Sends the draft to the designer queue. Returns a validation failure, without changing state, when a
    /// field the designer needs is missing.
    /// </summary>
    public Result Submit()
    {
        if (Status is not BriefingStatus.Draft)
            throw new DomainException($"Submission not allowed: the briefing is in {Status}.");

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

        Status = BriefingStatus.Submitted;
        return Result.Success();
    }

    /// <summary>Cancels the briefing. Only before a designer claims it.</summary>
    public void Cancel()
    {
        if (!IsOpenForRequester)
            throw new DomainException($"Cancellation not allowed: the briefing is in {Status}.");

        Status = BriefingStatus.Cancelled;
    }

    /// <summary>A designer takes the briefing from the queue and becomes responsible for it.</summary>
    public void Claim(Guid designerId)
    {
        if (designerId == Guid.Empty)
            throw new DomainException("The designer is required.");

        if (Status is not BriefingStatus.Submitted)
            throw new DomainException($"Claim not allowed: the briefing is in {Status}. Only submitted briefings can be claimed.");

        DesignerId = designerId;
        Status = BriefingStatus.InProgress;
    }

    /// <summary>
    /// The assigned designer hands over the work. The note is mandatory — it is the only thing the
    /// requester receives. Clears a pending change request, which this delivery answers.
    /// </summary>
    public void Deliver(Guid designerId, string? deliveryNote)
    {
        if (Status is not BriefingStatus.InProgress)
            throw new DomainException($"Delivery not allowed: the briefing is in {Status}. Only briefings in progress can be delivered.");

        if (designerId != DesignerId)
            throw new DomainException("Only the designer who claimed the briefing can deliver it.");

        if (string.IsNullOrWhiteSpace(deliveryNote))
            throw new DomainException("Delivery note is required.");

        var note = deliveryNote.Trim();
        if (note.Length > DeliveryNoteMaxLength)
            throw new DomainException($"Delivery note must be at most {DeliveryNoteMaxLength} characters.");

        DeliveryNote = note;
        DeliveredAt = DateTime.UtcNow;
        ChangeRequestReason = null;
        Status = BriefingStatus.Delivered;
    }

    /// <summary>The requester accepts the delivery and closes the briefing.</summary>
    public void Accept()
    {
        EnsureDelivered("Acceptance");

        Status = BriefingStatus.Completed;
    }

    /// <summary>
    /// The requester sends the delivery back to the same designer with what to change. The reason is
    /// mandatory; an absent, blank or oversized one throws and leaves the status untouched.
    /// </summary>
    public void RequestChanges(string? reason)
    {
        EnsureDelivered("Change request");

        var justification = Justification.Create(reason);
        if (justification.IsFailure)
            throw new DomainException(justification.Error ?? "Justification is required.");

        ChangeRequestReason = justification.Value;
        Status = BriefingStatus.InProgress;
    }

    private void EnsureDelivered(string operation)
    {
        if (Status is not BriefingStatus.Delivered)
            throw new DomainException(
                $"{operation} not allowed: the briefing is in {Status}. Only delivered briefings can be reviewed.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
