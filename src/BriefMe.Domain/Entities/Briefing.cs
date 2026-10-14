using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;

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

        Title = title.Trim();
        Priority = priority;
        Description = Normalize(description);
        Objective = Normalize(objective);
        RequestingArea = Normalize(requestingArea);
        DesiredDeliveryDate = desiredDeliveryDate;
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
        return Result.Success();
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

        Status = BriefingStatus.Cancelled;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
