using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.Services;

namespace BriefMe.Application.UseCases.Sla.MonitorDeadlines;

/// <summary>
/// Sweeps the open demands, marks the ones past their due instant and publishes <c>DeadlineOverdue</c> for
/// them (RF028, BRF-043) — the use case the SLA hosted service runs on a timer.
/// </summary>
/// <remarks>
/// Never re-judges a demand it already marked: the repositories filter <c>OverdueSince IS NULL</c>, which is
/// what turns "published exactly once" into a property of the query rather than a promise of the dispatcher.
/// The way back into the sweep is replanning, which clears the mark
/// (<see cref="BriefingTask.SetDeadlineAndPriority"/>, <see cref="Briefing.Edit"/>).
/// <para>
/// One commit for the whole pass, which is also what publishes: <c>UnitOfWork</c> collects what the marked
/// aggregates raised and dispatches it after the write (BRF-049), so a notification (RF035, BRF-053) can
/// never describe a breach that is not yet stored. Nothing is committed when nothing was marked — the steady
/// state of a healthy system, which should not write to the database every fifteen minutes.
/// </para>
/// <para>
/// The pass takes no <c>ICurrentUser</c>: it runs outside any request. The audit interceptor (RF038) will
/// stamp <c>UpdatedAt</c> and leave <c>UpdatedBy</c> null, which reads correctly as "the system did this".
/// </para>
/// </remarks>
public sealed class MonitorDeadlinesCommandHandler(
    IBriefingRepository briefingRepository,
    IBriefingTaskRepository taskRepository,
    ISlaRuleRepository slaRuleRepository,
    SlaDeadlineResolver deadlineResolver,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<MonitorDeadlinesCommand, SlaMonitorRunDto>
{
    public async Task<Result<SlaMonitorRunDto>> Handle(
        MonitorDeadlinesCommand request,
        CancellationToken cancellationToken)
    {
        // One instant for the whole pass: two demands due a millisecond apart must not be judged against
        // different clocks, and the value is what lands in OverdueSince.
        var evaluatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        // The whole policy, once. Every demand of the pass resolves its rule against this same set (RF027).
        var rules = await slaRuleRepository.GetAllAsync(cancellationToken);

        var markedAggregates = new List<AggregateRoot>();

        var tasks = await SweepTasksAsync(request, rules, evaluatedAtUtc, markedAggregates, cancellationToken);
        var briefings = await SweepBriefingsAsync(request, rules, evaluatedAtUtc, markedAggregates, cancellationToken);

        // No Update calls: the aggregates came from the DbContext and are tracked, so CommitAsync emits the
        // UPDATEs — the same reasoning as CompleteBriefingWhenAllTasksApprovedHandler. The commit also
        // publishes their DeadlineOverdue events (BRF-049); the list is kept only to know whether the pass
        // has anything to write at all.
        if (markedAggregates.Count > 0)
            await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new SlaMonitorRunDto(
            evaluatedAtUtc,
            tasks.Scanned,
            tasks.Marked,
            briefings.Scanned,
            briefings.Marked));
    }

    /// <summary>
    /// Judges the open, unmarked tasks (RF029). A task has no category of its own, so its SLA scope comes
    /// from the briefing it belongs to — loaded one batch at a time rather than one task at a time.
    /// </summary>
    private async Task<(int Scanned, int Marked)> SweepTasksAsync(
        MonitorDeadlinesCommand request,
        IReadOnlyList<SlaRule> rules,
        DateTime evaluatedAtUtc,
        List<AggregateRoot> markedAggregates,
        CancellationToken cancellationToken)
    {
        var markedBefore = markedAggregates.Count;
        var scanned = 0;

        for (var page = 1; ; page++)
        {
            var batch = await taskRepository.GetOverdueCandidatesAsync(page, request.BatchSize, cancellationToken);

            if (batch.Count == 0)
                break;

            var briefings = (await briefingRepository.GetByIdsAsync(
                    [.. batch.Select(task => task.BriefingId).Distinct()],
                    cancellationToken))
                .ToDictionary(briefing => briefing.Id);

            foreach (var task in batch)
            {
                scanned++;

                // The FK makes an orphan task impossible, but one unreadable row must not cost the pass the
                // demands that come after it.
                if (!briefings.TryGetValue(task.BriefingId, out var briefing))
                    continue;

                // The task's own priority (RF019), which the Design Manager may have set apart from the
                // briefing's; the briefing's category, which the task borrows (RF040). The SLA clock starts
                // at creation: a task only exists for an approved briefing (RF015).
                var dueAt = deadlineResolver.ResolveDueAt(
                    rules,
                    briefing.CategoryId,
                    task.Priority,
                    task.CreatedAt,
                    task.Deadline);

                if (dueAt is null || dueAt > evaluatedAtUtc)
                    continue;

                task.MarkOverdue(dueAt.Value, evaluatedAtUtc);
                markedAggregates.Add(task);
            }

            // A short page is the last one. The ceiling stops a pass from loading an unbounded backlog into
            // memory; what is left is swept next time, since marking is incremental.
            if (batch.Count < request.BatchSize || scanned >= request.MaxItemsPerRun)
                break;
        }

        return (scanned, markedAggregates.Count - markedBefore);
    }

    /// <summary>Judges the open, unmarked briefings (RF030). Same shape as the task sweep.</summary>
    private async Task<(int Scanned, int Marked)> SweepBriefingsAsync(
        MonitorDeadlinesCommand request,
        IReadOnlyList<SlaRule> rules,
        DateTime evaluatedAtUtc,
        List<AggregateRoot> markedAggregates,
        CancellationToken cancellationToken)
    {
        var markedBefore = markedAggregates.Count;
        var scanned = 0;

        for (var page = 1; ; page++)
        {
            var batch = await briefingRepository.GetOverdueCandidatesAsync(page, request.BatchSize, cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var briefing in batch)
            {
                scanned++;

                var dueAt = deadlineResolver.ResolveDueAt(
                    rules,
                    briefing.CategoryId,
                    briefing.Priority,
                    SlaStartOf(briefing),
                    briefing.DesiredDeliveryDate);

                if (dueAt is null || dueAt > evaluatedAtUtc)
                    continue;

                briefing.MarkOverdue(dueAt.Value, evaluatedAtUtc);
                markedAggregates.Add(briefing);
            }

            if (batch.Count < request.BatchSize || scanned >= request.MaxItemsPerRun)
                break;
        }

        return (scanned, markedAggregates.Count - markedBefore);
    }

    /// <summary>
    /// When the design sector took the briefing on, which is where its SLA clock starts: the moment of
    /// approval (RF011). Null while the demand is not the sector's to deliver — a briefing awaiting review or
    /// returned for corrections is still with the requester, and only its requested date applies.
    /// </summary>
    /// <remarks>
    /// <see cref="Briefing.ReviewedAt"/> alone would not do: it also records a rejection or a change request,
    /// so it is read only in the two statuses that follow an approval.
    /// </remarks>
    private static DateTime? SlaStartOf(Briefing briefing) =>
        briefing.Status is BriefingStatus.Approved or BriefingStatus.InProgress
            ? briefing.ReviewedAt
            : null;
}
