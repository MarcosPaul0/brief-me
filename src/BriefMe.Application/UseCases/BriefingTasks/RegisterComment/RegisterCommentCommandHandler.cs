using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.UseCases.BriefingTasks.RegisterComment;

/// <summary>
/// Records a comment on a task (RF021, BRF-030). Loads the task, checks the caller is involved in it, and
/// delegates the writing to <c>BriefingTask.AddComment()</c> — <c>Comment</c> is a child of this aggregate
/// (BRF-024), so it is created through the root and never through a repository of its own.
/// </summary>
/// <remarks>
/// The status of the task is deliberately not consulted: commenting is conversation, not a state change
/// (RF020), and a blocked or rejected task is exactly when the people involved need to talk (RF048).
/// <para>
/// Authorization is data, not permission: whether the caller may comment at all is the <c>tasks.comment</c>
/// policy on the endpoint (BRF-032); whether they are involved in <b>this</b> task depends on the loaded
/// <c>DesignerId</c> and on the requester of the parent briefing, which only this layer can read. Same shape
/// as <c>UpdateTaskStatusCommandHandler</c>.
/// </para>
/// </remarks>
public sealed class RegisterCommentCommandHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterCommentCommand, CommentDto>
{
    /// <summary>
    /// Profiles involved in every task by role rather than by assignment: design management drives the
    /// pipeline (RF015–RF019, RF043) and the administrator holds the whole catalog (USR-047). Same list as
    /// BRF-029.
    /// </summary>
    private static readonly string[] ManagementRoles =
    [
        DomainRoles.GestorDeDesign,
        DomainRoles.Administrador
    ];

    public async Task<Result<CommentDto>> Handle(
        RegisterCommentCommand request,
        CancellationToken cancellationToken)
    {
        // The caller is the comment's author, so an unauthenticated one has nothing to record and must not
        // reach the repositories.
        if (!Guid.TryParse(currentUser.UserId, out var authorId))
            return Result.Unauthorized<CommentDto>(CommonErrors.UserNotAuthenticated);

        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<CommentDto>(BriefingTaskErrors.NotFound);

        if (!await CallerIsInvolvedAsync(task, authorId, cancellationToken))
            return Result.Forbidden<CommentDto>(BriefingTaskErrors.NotInvolvedInTask);

        // Text is re-validated inside Comment.Create (BRF-024): the validator answers user input with a 400,
        // the aggregate keeps the invariant regardless of who calls it.
        var comment = task.AddComment(authorId, request.Text);

        // No Update call: the task is tracked by the DbContext, so the new child of its Comments collection
        // is picked up by change detection and CommitAsync emits the INSERT.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(CommentDto.FromComment(comment));
    }

    /// <summary>
    /// "The involved" of RF021: the assigned designer, the requester who opened the parent briefing, or
    /// someone with a management profile. Checked in that order and lazily — the assignee, the common case,
    /// costs no extra query; the profile catalog is only read for a caller who is neither assignee nor
    /// requester.
    /// </summary>
    /// <remarks>
    /// Profiles are matched by id — the whole role catalog is four rows (USR-047) — rather than through the
    /// <c>UserRole.Role</c> navigation, which is only populated when the query included it. Same precedent as
    /// <c>AssignTaskCommandHandler</c> and <c>UpdateTaskStatusCommandHandler</c>.
    /// </remarks>
    private async Task<bool> CallerIsInvolvedAsync(
        BriefingTask task,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        if (task.DesignerId == callerId)
            return true;

        // A task whose briefing no longer exists has no requester to match; the caller may still be management.
        var briefing = await briefingRepository.GetByIdAsync(task.BriefingId, cancellationToken);
        if (briefing?.RequesterId == callerId)
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
