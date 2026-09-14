using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.Services;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingTracking;

/// <summary>
/// Reads how far a briefing has come and what it was broken into (BRF-039, RF023): the briefing's header,
/// the progress <see cref="BriefingProgressCalculator"/> computes (BRF-035) and its tasks in decomposition
/// order, in one round trip.
/// </summary>
/// <remarks>
/// The percentage is calculated here and nowhere else — the screen renders the number it is handed, so the
/// rule of RF023/RF059 has a single home. Nothing is cached: the value is derived from the tasks that exist
/// at the moment it is asked for, which is why <c>GetByBriefingIdAsync</c> is unpaginated (BRF-025).
/// <para>
/// Authorization splits the same way as in <c>GetTaskDetailQueryHandler</c>: whether the caller may read
/// briefing progress at all is the permission on the endpoint (BRF-039 gates on <c>briefings.view-own</c>
/// or <c>briefings.view-all</c>); whether they are involved in <b>this</b> briefing is data only this layer
/// can read. Involvement is wider than ownership here on purpose — design management approves the briefing
/// (RF011) and creates its tasks (RF015), so it tracks what it runs.
/// </para>
/// </remarks>
public sealed class GetBriefingTrackingQueryHandler(
    IBriefingRepository briefingRepository,
    IBriefingTaskRepository taskRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    BriefingProgressCalculator progressCalculator,
    ICurrentUser currentUser) : IQueryHandler<GetBriefingTrackingQuery, BriefingTrackingDto>
{
    /// <summary>
    /// Profiles involved in every briefing by role rather than by ownership: design management drives the
    /// pipeline (RF011, RF015–RF019) and the administrator holds the whole catalog (USR-047). Same list as
    /// BRF-029/030/031/034.
    /// </summary>
    private static readonly string[] ManagementRoles =
    [
        DomainRoles.GestorDeDesign,
        DomainRoles.Administrador
    ];

    public async Task<Result<BriefingTrackingDto>> Handle(
        GetBriefingTrackingQuery request,
        CancellationToken cancellationToken)
    {
        // The caller is the filter, so an unauthenticated context has nothing to read and must not reach
        // the repositories.
        if (!Guid.TryParse(currentUser.UserId, out var callerId))
            return Result.Unauthorized<BriefingTrackingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingTrackingDto>(BriefingErrors.NotFound);

        if (!await CallerIsInvolvedAsync(briefing, callerId, cancellationToken))
            return Result.Forbidden<BriefingTrackingDto>(BriefingErrors.NotInvolvedToTrack);

        // Every task of the briefing, unpaginated: a truncated page would yield a wrong percentage.
        var tasks = await taskRepository.GetByBriefingIdAsync(briefing.Id, cancellationToken);

        var progress = progressCalculator.Calculate(tasks);

        var personNames = await GetPersonNamesAsync(briefing, tasks, cancellationToken);

        return Result.Success(BriefingTrackingDto.FromBriefingWithProgressAndTasks(
            briefing,
            progress,
            tasks,
            personNames));
    }

    /// <summary>
    /// Who may track a briefing: the requester who opened it, or someone with a management profile. Checked
    /// in that order and lazily — the requester, the common case, costs no extra query, and the profile
    /// catalog is only read for a caller who is not the owner.
    /// </summary>
    private async Task<bool> CallerIsInvolvedAsync(
        Briefing briefing,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        if (briefing.RequesterId == callerId)
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
    /// Display names of everyone the screen shows — the requester in the header and each task's assignee —
    /// keyed by id, in one query. A designer holding three tasks of the briefing is read once, and a name
    /// that does not resolve degrades a single cell.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetPersonNamesAsync(
        Briefing briefing,
        IReadOnlyList<BriefingTask> tasks,
        CancellationToken cancellationToken)
    {
        var personIds = tasks
            .Where(task => task.DesignerId is not null)
            .Select(task => task.DesignerId!.Value)
            .ToHashSet();

        personIds.Add(briefing.RequesterId);

        var people = await userRepository.GetByIdsAsync(personIds, cancellationToken);

        return people.ToDictionary(person => person.Id, person => person.FullName);
    }
}
