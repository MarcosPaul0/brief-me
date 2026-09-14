using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;

/// <summary>
/// Sets a task's deadline (RF018) and priority (RF019), BRF-028. Loads the task, checks it can still be
/// replanned, and delegates the change to <c>BriefingTask.SetDeadlineAndPriority()</c>.
/// </summary>
/// <remarks>
/// Like the other Design Manager handlers there is <b>no ownership guard</b> — planning someone else's work is
/// the point. Whether the caller may plan at all is decided by the policy on the endpoint (BRF-032);
/// <c>ICurrentUser</c> exposes no permissions.
/// <para>
/// The deadline is not re-checked against today here: a past date is a validation failure (400), already
/// caught by <see cref="SetDeadlinePriorityCommandValidator"/> before the handler runs, and re-stated as an
/// invariant inside the aggregate. What the handler owns is the part that needs the database — the task's
/// status.
/// </para>
/// </remarks>
public sealed class SetDeadlinePriorityCommandHandler(
    IBriefingTaskRepository taskRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<SetDeadlinePriorityCommand, BriefingTaskDto>
{
    public async Task<Result<BriefingTaskDto>> Handle(
        SetDeadlinePriorityCommand request,
        CancellationToken cancellationToken)
    {
        // The caller becomes UpdatedBy through the auditing interceptor (RF038), so an unauthenticated caller
        // must not reach the repositories.
        if (!Guid.TryParse(currentUser.UserId, out _))
            return Result.Unauthorized<BriefingTaskDto>(CommonErrors.UserNotAuthenticated);

        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<BriefingTaskDto>(BriefingTaskErrors.NotFound);

        // Mirrors the invariant of SetDeadlineAndPriority(): checking it here turns an expected outcome (a
        // stale board, two open tabs) into a conflict instead of a DomainException surfacing as a 500
        // (CLAUDE.md — Result over exceptions for expected failures).
        if (task.Status is BriefingTaskStatus.Approved)
            return Result.Conflict<BriefingTaskDto>(
                BriefingTaskErrors.DeadlineAndPriorityChangeNotAllowed(task.Status));

        task.SetDeadlineAndPriority(request.Deadline, request.Priority);

        // No Update call: the task is tracked by the DbContext, so CommitAsync emits the UPDATE.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingTaskDto.FromBriefingTask(task));
    }
}
