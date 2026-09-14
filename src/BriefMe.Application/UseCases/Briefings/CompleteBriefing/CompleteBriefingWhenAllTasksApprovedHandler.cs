using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.Services;

namespace BriefMe.Application.UseCases.Briefings.CompleteBriefing;

/// <summary>
/// Closes a briefing as soon as its last task is approved (RF024, RF058, BRF-036). Reacts to
/// <see cref="TaskApprovedByRequester"/> — the requester's final approval of a delivery (RF046, BRF-069) —
/// and completes the briefing only when <b>every</b> one of its tasks is in
/// <see cref="BriefingTaskStatus.Approved"/>.
/// </summary>
/// <remarks>
/// An event handler rather than a step inside the approval use case: approving a delivery is an act on one
/// task, completing the briefing is a consequence on another aggregate. Folding the two together would give
/// the approval use case the briefing repository and the RF058 rule.
/// <para>
/// Nothing raises the event yet — <c>BriefingTask.ApproveByRequester</c> is BRF-065/BRF-069. The delivery is
/// in place since BRF-049: whichever commit raises it publishes it, and this handler is reached unchanged.
/// </para>
/// </remarks>
public sealed class CompleteBriefingWhenAllTasksApprovedHandler(
    IBriefingRepository briefingRepository,
    IBriefingTaskRepository taskRepository,
    BriefingProgressCalculator progressCalculator,
    IUnitOfWork unitOfWork) : IDomainEventHandler<TaskApprovedByRequester>
{
    public async Task Handle(TaskApprovedByRequester domainEvent, CancellationToken cancellationToken)
    {
        var briefing = await briefingRepository.GetByIdAsync(domainEvent.BriefingId, cancellationToken);

        // Anything but a briefing under execution has nothing to complete: already Completed means the event
        // was redelivered (delivery is at least once, BRF-049), and any other status is an anomaly. Both return
        // instead of throwing — a handler has no Result to hand back, and a DomainException escaping here
        // would poison the dispatcher rather than fix the briefing.
        if (briefing is null || briefing.Status is not BriefingStatus.InProgress)
            return;

        // Unpaginated on purpose (see IBriefingTaskRepository): a truncated page could make a briefing with
        // pending work look finished, which is exactly what RF058 forbids.
        var tasks = await taskRepository.GetByBriefingIdAsync(domainEvent.BriefingId, cancellationToken);

        // IsComplete counts approved tasks against the total (BRF-035) — the one definition of "all approved"
        // in the system. Never a Percentage == 100 comparison: that is a rounded display value.
        if (!progressCalculator.Calculate(tasks).IsComplete)
            return;

        briefing.Complete();

        // No Update call: the briefing is tracked by the DbContext, so CommitAsync emits the UPDATE. The
        // handler runs after the approving transaction committed (BRF-049), so it owns this one — and the
        // nested commit publishes only what completing the briefing raised.
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
