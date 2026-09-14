using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;

/// <summary>
/// Creates the tasks of an approved briefing (RF015, RF016) and puts the briefing under execution
/// (BRF-026). The whole batch shares one <c>CommitAsync</c>: either every task and the status transition are
/// persisted, or nothing is.
/// </summary>
/// <remarks>
/// Like the other Design Manager handlers, there is <b>no ownership guard</b> — decomposing someone else's
/// briefing is the point. Whether the caller may create tasks at all is decided by the <c>tasks.create</c>
/// policy on the endpoint (BRF-032); <c>ICurrentUser</c> exposes no permissions.
/// </remarks>
public sealed class CreateBriefingTasksCommandHandler(
    IBriefingRepository briefingRepository,
    IBriefingTaskRepository taskRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateBriefingTasksCommand, IReadOnlyList<BriefingTaskDto>>
{
    public async Task<Result<IReadOnlyList<BriefingTaskDto>>> Handle(
        CreateBriefingTasksCommand request,
        CancellationToken cancellationToken)
    {
        // Read even though no task field stores it: the auditing interceptor turns it into CreatedBy (RF038),
        // and an unauthenticated caller must not reach the repository.
        if (!Guid.TryParse(currentUser.UserId, out _))
            return Result.Unauthorized<IReadOnlyList<BriefingTaskDto>>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<IReadOnlyList<BriefingTaskDto>>(BriefingErrors.NotFound);

        // RF015 allows tasks only for an approved briefing. InProgress counts as approved here: the first
        // task moves the briefing there, and the Manager may keep decomposing it afterwards. Reviewing a
        // stale screen is an expected outcome, not a bug — a conflict, never a 500 from the aggregate
        // (CLAUDE.md: Result over exceptions for expected failures).
        if (briefing.Status is not (BriefingStatus.Approved or BriefingStatus.InProgress))
            return Result.Conflict<IReadOnlyList<BriefingTaskDto>>(BriefingTaskErrors.CreationNotAllowed(briefing.Status));

        var tasks = new List<BriefingTask>(request.Tasks.Count);

        foreach (var item in request.Tasks)
        {
            var task = BriefingTask.Create(briefing.Id, item.Title, item.Priority);
            await taskRepository.AddAsync(task, cancellationToken);
            tasks.Add(task);
        }

        // Execution has started (RF010: the briefing can no longer be cancelled). No Update call: the
        // briefing is tracked by the DbContext, so the transition rides along in the same SaveChanges.
        briefing.StartExecution();

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BriefingTaskDto>>([.. tasks.Select(BriefingTaskDto.FromBriefingTask)]);
    }
}
