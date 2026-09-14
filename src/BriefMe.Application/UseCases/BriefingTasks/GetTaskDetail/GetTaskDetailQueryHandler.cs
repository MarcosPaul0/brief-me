using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.UseCases.BriefingTasks.GetTaskDetail;

/// <summary>
/// Reads the record behind one task (BRF-034): its planning data, its comment trail (RF021) and the files
/// delivered on it with the author and moment RF052 asks for. The read the two existing write endpoints
/// (BRF-030, BRF-031) never had.
/// </summary>
/// <remarks>
/// Authorization here is data, not permission: whether the caller may read tasks at all is the policy on
/// the endpoint (BRF-032); whether they are involved in <b>this</b> one depends on the loaded
/// <c>DesignerId</c> and on the requester of the parent briefing, which only this layer can read. Same
/// three-way rule and same shape as <c>RegisterCommentCommandHandler</c> — RF021's "involved" is exactly
/// the set that may also read what was said.
/// <para>
/// Every person on the page — comment authors, delivery uploaders and the assignee — is resolved in a
/// single batched <c>GetByIdsAsync</c>, so a task with forty comments from three people costs one lookup
/// (the precedent of BRF-020 and BRF-033). A name that does not resolve degrades one cell instead of
/// failing the read.
/// </para>
/// </remarks>
public sealed class GetTaskDetailQueryHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ICurrentUser currentUser) : IQueryHandler<GetTaskDetailQuery, TaskDetailDto>
{
    /// <summary>
    /// Profiles involved in every task by role rather than by assignment: design management drives the
    /// pipeline (RF015–RF019, RF043) and the administrator holds the whole catalog (USR-047). Same list as
    /// BRF-029/030/031.
    /// </summary>
    private static readonly string[] ManagementRoles =
    [
        DomainRoles.GestorDeDesign,
        DomainRoles.Administrador
    ];

    public async Task<Result<TaskDetailDto>> Handle(
        GetTaskDetailQuery request,
        CancellationToken cancellationToken)
    {
        // The caller is the filter, so an unauthenticated context has nothing to read and must not reach
        // the repositories.
        if (!Guid.TryParse(currentUser.UserId, out var callerId))
            return Result.Unauthorized<TaskDetailDto>(CommonErrors.UserNotAuthenticated);

        var task = await taskRepository.GetByIdWithCommentsAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<TaskDetailDto>(BriefingTaskErrors.NotFound);

        // Read once and reused twice: it answers whether the caller is the requester, and it carries the
        // title the header shows. A task whose briefing is gone still renders — the title degrades to null.
        var briefing = await briefingRepository.GetByIdAsync(task.BriefingId, cancellationToken);

        if (!await CallerIsInvolvedAsync(task, briefing, callerId, cancellationToken))
            return Result.Forbidden<TaskDetailDto>(BriefingTaskErrors.NotInvolvedToView);

        var deliveries = await briefingRepository.GetDeliveriesByTaskAsync(task.Id, cancellationToken);

        var personNames = await GetPersonNamesAsync(task, deliveries, cancellationToken);

        return Result.Success(TaskDetailDto.FromBriefingTaskWithComplements(
            task,
            deliveries,
            personNames,
            briefing?.Title));
    }

    /// <summary>
    /// "The involved" of RF021: the assigned designer, the requester who opened the parent briefing, or
    /// someone with a management profile. Checked in that order and lazily — the assignee, the common case,
    /// costs no extra query, and the profile catalog is only read for a caller who is neither.
    /// </summary>
    private async Task<bool> CallerIsInvolvedAsync(
        BriefingTask task,
        Briefing? briefing,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        if (task.DesignerId == callerId)
            return true;

        // A task whose briefing no longer exists has no requester to match; the caller may still be management.
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

    /// <summary>
    /// Display names of everyone the page shows — comment authors, delivery uploaders and the assignee —
    /// keyed by id. Distinct ids only, in one query: a designer who left ten comments is read once.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetPersonNamesAsync(
        BriefingTask task,
        IReadOnlyList<Attachment> deliveries,
        CancellationToken cancellationToken)
    {
        var personIds = task.Comments
            .Select(comment => comment.AuthorId)
            .Concat(deliveries.Select(delivery => delivery.UploadedById))
            .ToHashSet();

        if (task.DesignerId is { } designerId)
            personIds.Add(designerId);

        var people = await userRepository.GetByIdsAsync(personIds, cancellationToken);

        return people.ToDictionary(person => person.Id, person => person.FullName);
    }
}
