using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Sla.GetOverdueTasks;

/// <summary>
/// Reads the overdue task list (RF029, BRF-044): one page of the tasks the deadline monitor marked and that
/// are still open, longest late first, each carrying the deadline it broke, its assignee and the briefing it
/// serves.
/// </summary>
/// <remarks>
/// Lateness is not recomputed here. The monitor (BRF-043) resolved the applicable SLA rule (RF027), decided
/// the effective due instant and stored the mark; this query reports it. Deciding it a second time would
/// need the whole policy loaded per request and could contradict the <c>DeadlineOverdue</c> already
/// published (RF035) if a rule changed in between.
/// <para>
/// Both display names are resolved server-side for the same reason the approval panel resolves its own
/// (BRF-020): the Design Manager reading this list holds neither <c>users.view</c> nor a briefing scope wide
/// enough to look them up from the browser. Each is a single batched <c>GetByIdsAsync</c>, so the page costs
/// a constant number of queries whatever its size, and an unresolved name degrades one cell rather than the
/// whole list.
/// </para>
/// </remarks>
public sealed class GetOverdueTasksQueryHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    IUserRepository userRepository) : IQueryHandler<GetOverdueTasksQuery, PagedDto<OverdueTaskDto>>
{
    public async Task<Result<PagedDto<OverdueTaskDto>>> Handle(
        GetOverdueTasksQuery request,
        CancellationToken cancellationToken)
    {
        var tasks = await taskRepository.GetOverdueAsync(request.Page, request.PageSize, cancellationToken);
        var totalCount = await taskRepository.CountOverdueAsync(cancellationToken);

        var briefingTitles = await GetBriefingTitlesAsync(tasks, cancellationToken);
        var designerNames = await GetDesignerNamesAsync(tasks, cancellationToken);

        var items = tasks
            .Select(task => OverdueTaskDto.FromBriefingTaskWithComplements(task, briefingTitles, designerNames))
            .ToList();

        return Result.Success(PagedDto<OverdueTaskDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }

    /// <summary>
    /// Titles of the briefings behind this page's tasks, keyed by id. Distinct ids only: a briefing whose
    /// five tasks are all late is read once.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetBriefingTitlesAsync(
        IReadOnlyList<BriefingTask> tasks,
        CancellationToken cancellationToken)
    {
        var briefingIds = tasks.Select(task => task.BriefingId).Distinct().ToList();

        var briefings = await briefingRepository.GetByIdsAsync(briefingIds, cancellationToken);

        return briefings.ToDictionary(briefing => briefing.Id, briefing => briefing.Title);
    }

    /// <summary>
    /// Names of the assignees on this page, keyed by id. Unassigned tasks (RF017) contribute nothing to the
    /// lookup, and an empty set skips the query altogether.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetDesignerNamesAsync(
        IReadOnlyList<BriefingTask> tasks,
        CancellationToken cancellationToken)
    {
        var designerIds = tasks
            .Where(task => task.DesignerId is not null)
            .Select(task => task.DesignerId!.Value)
            .Distinct()
            .ToList();

        if (designerIds.Count == 0)
            return [];

        var designers = await userRepository.GetByIdsAsync(designerIds, cancellationToken);

        return designers.ToDictionary(user => user.Id, user => user.FullName);
    }
}
