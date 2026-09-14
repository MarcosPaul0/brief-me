using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;

/// <summary>
/// Moves a task through the execution states of RF020 (BRF-029). Loads the task, checks the caller owns it,
/// checks the requested state is one this command may set, and delegates the move to
/// <c>BriefingTask.UpdateStatus()</c> — the RF020 table of BRF-023 stays the only definition of which edges
/// exist.
/// </summary>
/// <remarks>
/// Unlike the Design Manager handlers (BRF-026/027/028) this one <b>does</b> guard ownership: RF020 is the
/// assignee's statement about their own work, and the endpoint policy (BRF-032) cannot check it because
/// whether this task is the caller's is data, not a permission. A Design Manager (or an Administrator) may
/// drive any task, as BRF-029 states; that costs one extra read, and only for callers who are not the
/// assignee.
/// <para>
/// Only the task named in the command is loaded — no sibling of the same briefing is read or touched (RF057).
/// </para>
/// </remarks>
public sealed class UpdateTaskStatusCommandHandler(
    IBriefingTaskRepository taskRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateTaskStatusCommand, BriefingTaskDto>
{
    /// <summary>
    /// The states the person doing the work declares by themselves. The other four of RF020 are the outcome
    /// of the delivery approval flow (RF042–RF048) and are reached through BRF-066/067/069, which enforce the
    /// attached delivery, the justification and the approver's profile that this command cannot.
    /// </summary>
    private static readonly BriefingTaskStatus[] ExecutionStatuses =
    [
        BriefingTaskStatus.NotStarted,
        BriefingTaskStatus.InProgress,
        BriefingTaskStatus.Blocked
    ];

    /// <summary>Profiles allowed to drive a task they are not assigned to (BRF-029).</summary>
    private static readonly string[] ManagementRoles =
    [
        DomainRoles.GestorDeDesign,
        DomainRoles.Administrador
    ];

    public async Task<Result<BriefingTaskDto>> Handle(
        UpdateTaskStatusCommand request,
        CancellationToken cancellationToken)
    {
        // The caller becomes UpdatedBy through the auditing interceptor (RF038) and is the subject of the
        // ownership guard below, so an unauthenticated caller must not reach the repositories.
        if (!Guid.TryParse(currentUser.UserId, out var callerId))
            return Result.Unauthorized<BriefingTaskDto>(CommonErrors.UserNotAuthenticated);

        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<BriefingTaskDto>(BriefingTaskErrors.NotFound);

        if (!await CallerMayUpdateAsync(task, callerId, cancellationToken))
            return Result.Forbidden<BriefingTaskDto>(BriefingTaskErrors.NotAssignedToCaller);

        // The state is a legitimate enum value — it just is not reachable through this door.
        if (!ExecutionStatuses.Contains(request.Status))
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.StatusNotSetDirectly(request.Status));

        // Mirrors the invariant of UpdateStatus(): checking it here turns an expected outcome (a stale board,
        // two open tabs) into a conflict instead of a DomainException surfacing as a 500
        // (CLAUDE.md — Result over exceptions for expected failures).
        if (!task.CanTransitionTo(request.Status))
            return Result.Conflict<BriefingTaskDto>(
                BriefingTaskErrors.StatusTransitionNotAllowed(task.Status, request.Status));

        task.UpdateStatus(request.Status);

        // No Update call: the task is tracked by the DbContext, so CommitAsync emits the UPDATE.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingTaskDto.FromBriefingTask(task));
    }

    /// <summary>
    /// The assignee (RF020) or someone with a management profile. The profile lookup only happens for callers
    /// who are not the assignee, so the common path costs no extra query.
    /// </summary>
    /// <remarks>
    /// Profiles are matched by id — the whole role catalog is four rows (USR-047) — rather than through the
    /// <c>UserRole.Role</c> navigation, which is only populated when the query included it. Same precedent as
    /// <c>AssignTaskCommandHandler</c>.
    /// </remarks>
    private async Task<bool> CallerMayUpdateAsync(
        BriefingTask task,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        if (task.DesignerId == callerId)
            return true;

        // Soft-deleted users are filtered out by the global query filter, so a deleted caller reads as null
        // and simply holds no management profile.
        var caller = await userRepository.GetByIdWithRolesAsync(callerId, cancellationToken);
        if (caller is null)
            return false;

        var roles = await roleRepository.GetAllAsync(cancellationToken);

        var managementRoleIds = roles
            .Where(role => ManagementRoles.Contains(role.Name))
            .Select(role => role.Id)
            .ToHashSet();

        return caller.UserRoles.Any(userRole => managementRoleIds.Contains(userRole.RoleId));
    }
}
