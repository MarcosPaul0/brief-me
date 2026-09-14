using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.BriefingTasks.ReopenTask;

/// <summary>
/// Reopens an approved task (RF025, BRF-037). Loads the task and its briefing, checks both can take the move,
/// and delegates each change to its own aggregate — <c>BriefingTask.Reopen()</c> and
/// <c>Briefing.Reopen()</c> — committing the two together.
/// </summary>
/// <remarks>
/// Like the other Design Manager handlers there is <b>no ownership guard</b>: RF025 is permission, not data —
/// whoever may reopen may reopen any task — and the <c>tasks.reopen</c> policy on the endpoint (BRF-032)
/// decides it. That differs from RF020 (BRF-029), where "is this task mine?" can only be answered after
/// loading the task.
/// <para>
/// Both aggregates move in a single <c>CommitAsync</c>. Splitting them would leave a window with a reopened
/// task under a <c>Completed</c> briefing — exactly the state RF058 forbids — and the corrected delivery
/// would then be refused, since <c>Briefing.AddDelivery</c> only accepts files while the briefing is in
/// execution (RF022, BRF-031). This is the inverse of BRF-036, which reaches the two aggregates through an
/// event precisely because approving a delivery and completing a briefing are two separate acts.
/// </para>
/// <para>
/// Only the task named in the command is loaded — no sibling of the same briefing is read or touched (RF057).
/// </para>
/// </remarks>
public sealed class ReopenTaskCommandHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<ReopenTaskCommand, BriefingTaskDto>
{
    public async Task<Result<BriefingTaskDto>> Handle(ReopenTaskCommand request, CancellationToken cancellationToken)
    {
        // The caller becomes UpdatedBy through the auditing interceptor (RF038), so an unauthenticated caller
        // must not reach the repositories.
        if (!Guid.TryParse(currentUser.UserId, out _))
            return Result.Unauthorized<BriefingTaskDto>(CommonErrors.UserNotAuthenticated);

        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<BriefingTaskDto>(BriefingTaskErrors.NotFound);

        // Mirrors the invariant of Reopen(): checking it here turns an expected outcome (a stale board, two
        // open tabs, the same task reopened twice) into a conflict instead of a DomainException surfacing as
        // a 500 (CLAUDE.md — Result over exceptions for expected failures).
        if (task.Status is not BriefingTaskStatus.Approved)
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.ReopenNotAllowed(task.Status));

        var briefing = await briefingRepository.GetByIdAsync(task.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingTaskDto>(BriefingErrors.NotFound);

        // InProgress means other tasks were still pending, so the briefing never completed; Completed means
        // this was the last one (BRF-036). No other status can hold an approved task, but a Result beats a
        // 500 if one ever does — and this guard runs before anything is mutated.
        if (briefing.Status is not (BriefingStatus.InProgress or BriefingStatus.Completed))
            return Result.Conflict<BriefingTaskDto>(BriefingErrors.ReopenNotAllowed(briefing.Status));

        task.Reopen();

        // Idempotent while the briefing is already InProgress, so no status check is needed around the call.
        briefing.Reopen();

        // No Update calls: both aggregates are tracked by the DbContext, so CommitAsync emits both UPDATEs —
        // in one transaction, which is what keeps RF058 true at every instant.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingTaskDto.FromBriefingTask(task));
    }
}
