using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.UseCases.BriefingTasks.ReassignTask;

/// <summary>
/// Hands a task to a different Designer (RF026, BRF-038). Loads the task and the target user, checks the
/// target really is an active Designer, and delegates the change to <c>BriefingTask.ReassignTo()</c>, which
/// raises <c>TaskAssigned</c> for the new owner (consumed later by Notifications — BRF-051, RF033).
/// </summary>
/// <remarks>
/// Like the other Design Manager handlers, there is <b>no ownership guard</b> — redistributing someone else's
/// work is the point. Whether the caller may transfer at all is decided by the <c>tasks.reassign</c> policy on
/// the endpoint (BRF-032); <c>ICurrentUser</c> exposes no permissions.
/// <para>
/// The target checks repeat those of <c>AssignTaskCommandHandler</c> deliberately: "who is a valid Designer"
/// has one source of truth (the role catalog, USR-047) and two readers, and the two handlers differ in the
/// task guards below — which is what each of them actually decides.
/// </para>
/// </remarks>
public sealed class ReassignTaskCommandHandler(
    IBriefingTaskRepository taskRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<ReassignTaskCommand, BriefingTaskDto>
{
    public async Task<Result<BriefingTaskDto>> Handle(ReassignTaskCommand request, CancellationToken cancellationToken)
    {
        // The caller rides into the domain event (AssignedById) and becomes UpdatedBy through the auditing
        // interceptor (RF038), so an unauthenticated caller must not reach the repositories.
        if (!Guid.TryParse(currentUser.UserId, out var reassignedById))
            return Result.Unauthorized<BriefingTaskDto>(CommonErrors.UserNotAuthenticated);

        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<BriefingTaskDto>(BriefingTaskErrors.NotFound);

        // The three guards below mirror invariants of ReassignTo(): checking them here turns an expected
        // outcome (a stale board, two open tabs) into a conflict instead of a DomainException surfacing as a
        // 500 (CLAUDE.md — Result over exceptions for expected failures).
        if (task.DesignerId is null)
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.NotAssigned);

        if (task.DesignerId == request.DesignerId)
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.AlreadyAssignedToDesigner);

        if (task.Status is BriefingTaskStatus.Approved)
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.ReassignmentNotAllowed(task.Status));

        // Soft-deleted users are filtered out by the global query filter, so a deleted id reads as not found.
        var designer = await userRepository.GetByIdWithRolesAsync(request.DesignerId, cancellationToken);
        if (designer is null)
            return Result.NotFound<BriefingTaskDto>(BriefingTaskErrors.DesignerNotFound);

        var designerRole = await roleRepository.GetByNameAsync(DomainRoles.Designer, cancellationToken);
        if (designerRole is null)
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.DesignerRoleNotConfigured);

        // RF026 transfers to another Designer; the role catalog (USR-047) is what makes someone one.
        if (!designer.UserRoles.Any(userRole => userRole.RoleId == designerRole.Id))
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.DesignerRoleRequired);

        if (designer.Status is not UserStatus.Active)
            return Result.Conflict<BriefingTaskDto>(BriefingTaskErrors.DesignerInactive);

        task.ReassignTo(designer.Id, reassignedById);

        // No Update call: the task is tracked by the DbContext, so CommitAsync emits the UPDATE.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingTaskDto.FromBriefingTask(task));
    }
}
