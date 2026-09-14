using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.BriefingTasks.GetTasksByDesigner;

/// <summary>
/// Reads the authenticated Designer's task board (RF017–RF020, BRF-033): one page of the tasks assigned to
/// them, nearest deadline first, enriched with the title of each task's parent briefing.
/// </summary>
/// <remarks>
/// The ordering is the repository's (BRF-025) and is not restated here — two definitions of "work plan
/// order" would drift at the first page boundary. The briefing titles are resolved server-side rather than
/// in the browser for the same reason the approval panel resolves its names (BRF-020): <c>GET /briefings</c>
/// is ownership-scoped, so a Designer can read none of the briefings whose tasks they hold. The lookup is a
/// single batched <c>GetByIdsAsync</c>, so the page costs a constant number of queries whatever its size,
/// and an unresolved title degrades one cell instead of failing the read.
/// </remarks>
public sealed class GetTasksByDesignerQueryHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser) : IQueryHandler<GetTasksByDesignerQuery, PagedDto<DesignerTaskDto>>
{
    public async Task<Result<PagedDto<DesignerTaskDto>>> Handle(
        GetTasksByDesignerQuery request,
        CancellationToken cancellationToken)
    {
        // The caller *is* the filter, so an unauthenticated context has nothing to read and must not reach
        // the repository with an empty designer id.
        if (!Guid.TryParse(currentUser.UserId, out var designerId))
            return Result.Unauthorized<PagedDto<DesignerTaskDto>>(CommonErrors.UserNotAuthenticated);

        var filter = new BriefingTaskFilter(request.Status, request.Priority);

        var tasks = await taskRepository.GetByDesignerAsync(
            designerId,
            filter,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await taskRepository.CountByDesignerAsync(designerId, filter, cancellationToken);

        var briefingTitles = await GetBriefingTitlesAsync(tasks, cancellationToken);

        var items = tasks
            .Select(task => DesignerTaskDto.FromBriefingTaskWithBriefingTitle(task, briefingTitles))
            .ToList();

        return Result.Success(PagedDto<DesignerTaskDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }

    /// <summary>
    /// Titles of the briefings behind this page's tasks, keyed by id. Distinct ids only: a briefing broken
    /// into five tasks is read once.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetBriefingTitlesAsync(
        IReadOnlyList<BriefingTask> tasks,
        CancellationToken cancellationToken)
    {
        var briefingIds = tasks.Select(task => task.BriefingId).Distinct().ToList();

        var briefings = await briefingRepository.GetByIdsAsync(briefingIds, cancellationToken);

        return briefings.ToDictionary(briefing => briefing.Id, briefing => briefing.Title);
    }
}
