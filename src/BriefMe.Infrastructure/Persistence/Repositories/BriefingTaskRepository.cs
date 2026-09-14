using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class BriefingTaskRepository(AppDbContext dbContext)
    : EfRepository<BriefingTask>(dbContext), IBriefingTaskRepository
{
    public async Task<IReadOnlyList<BriefingTask>> GetByBriefingIdAsync(
        Guid briefingId,
        CancellationToken cancellationToken = default) =>
        await DbContext.Tasks
            .Where(t => t.BriefingId == briefingId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<BriefingTask?> GetByIdWithCommentsAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        // Ordered inside the Include so the trail arrives in conversation order and no caller re-sorts it.
        await DbContext.Tasks
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BriefingTask>> GetByDesignerAsync(
        Guid designerId,
        BriefingTaskFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        // Nearest deadline first, undated tasks last; CreatedAt breaks ties so paging stays stable.
        await ByDesigner(designerId, filter)
            .OrderBy(t => t.Deadline == null)
            .ThenBy(t => t.Deadline)
            .ThenBy(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByDesignerAsync(
        Guid designerId,
        BriefingTaskFilter filter,
        CancellationToken cancellationToken = default) =>
        await ByDesigner(designerId, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<BriefingTask>> GetOverdueCandidatesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        // Id breaks ties on CreatedAt so the pages of one sweep never overlap or skip a row.
        await DbContext.Tasks
            .Where(t => t.OverdueSince == null && t.Status != BriefingTaskStatus.Approved)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BriefingTask>> GetOverdueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        // Longest late first. One sweep marks everything it finds with a single instant, so Id is what keeps
        // the pages of that batch from overlapping.
        await Overdue()
            .OrderBy(t => t.OverdueSince)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountOverdueAsync(CancellationToken cancellationToken = default) =>
        await Overdue().CountAsync(cancellationToken);

    /// <summary>
    /// The tasks marked late (BRF-043) that still need acting on. Approved work keeps its mark as a record
    /// of a late delivery, but is not part of the operational list of RF029.
    /// </summary>
    private IQueryable<BriefingTask> Overdue() =>
        DbContext.Tasks.Where(t => t.OverdueSince != null && t.Status != BriefingTaskStatus.Approved);

    private IQueryable<BriefingTask> ByDesigner(Guid designerId, BriefingTaskFilter filter)
    {
        var query = DbContext.Tasks.Where(t => t.DesignerId == designerId);

        if (filter.Status is not null)
            query = query.Where(t => t.Status == filter.Status);

        if (filter.Priority is not null)
            query = query.Where(t => t.Priority == filter.Priority);

        return query;
    }
}
