using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class BriefingRepository(AppDbContext dbContext) : EfRepository<Briefing>(dbContext), IBriefingRepository
{
    public async Task<IReadOnlyList<Briefing>> GetByRequesterAsync(
        Guid requesterId,
        BriefingFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await ByRequester(requesterId, filter)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByRequesterAsync(
        Guid requesterId,
        BriefingFilter filter,
        CancellationToken cancellationToken = default) =>
        await ByRequester(requesterId, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Briefing>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        // Short-circuit: an empty page has no briefings to resolve, and `IN ()` is not worth a round trip.
        if (ids.Count == 0)
            return [];

        return await DbContext.Briefings
            .Where(b => ids.Contains(b.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Briefing?> GetByIdWithAttachmentsAsync(Guid id, CancellationToken cancellationToken = default) =>
        // Filtered include: only the briefing's own material (TaskId == null), never the task deliveries
        // sharing the table. Ordering inside the include is what puts the newest file at the top of the list
        // the screen renders, so no caller has to re-sort the aggregate's collection.
        await DbContext.Briefings
            .Include(b => b.Attachments
                .Where(a => a.TaskId == null)
                .OrderByDescending(a => a.UploadedAt))
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Attachment>> GetDeliveriesByTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default) =>
        // Queried off the Attachments set rather than through the briefing's collection: loading every
        // attachment of the briefing to keep the handful belonging to one task would be paid per open task.
        await DbContext.Attachments
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Briefing>> GetPendingApprovalAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await PendingApproval()
            .OrderBy(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountPendingApprovalAsync(CancellationToken cancellationToken = default) =>
        await PendingApproval().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Briefing>> GetOverdueCandidatesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        // Id breaks ties on CreatedAt so the pages of one sweep never overlap or skip a row.
        await Open()
            .Where(b => b.OverdueSince == null)
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Briefing>> GetOverdueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        // Longest late first. One sweep marks everything it finds with a single instant, so Id is what keeps
        // the pages of that batch from overlapping.
        await Overdue()
            .OrderBy(b => b.OverdueSince)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountOverdueAsync(CancellationToken cancellationToken = default) =>
        await Overdue().CountAsync(cancellationToken);

    private IQueryable<Briefing> ByRequester(Guid requesterId, BriefingFilter filter)
    {
        var query = DbContext.Briefings.Where(b => b.RequesterId == requesterId);

        if (filter.Status is not null)
            query = query.Where(b => b.Status == filter.Status);

        if (filter.CategoryId is not null)
            query = query.Where(b => b.CategoryId == filter.CategoryId);

        return query;
    }

    private IQueryable<Briefing> PendingApproval() =>
        DbContext.Briefings.Where(b => b.Status == BriefingStatus.AwaitingApproval);

    /// <summary>
    /// The briefings the design sector still answers for. Draft was never submitted; Rejected, Cancelled and
    /// Completed are closed. Shared by the two deadline queries precisely so they agree on the scope: the
    /// monitor sweeps this set (BRF-043) and the delay list reports from it (BRF-045).
    /// </summary>
    private IQueryable<Briefing> Open() =>
        DbContext.Briefings
            .Where(b => b.Status == BriefingStatus.AwaitingApproval
                || b.Status == BriefingStatus.ChangesRequested
                || b.Status == BriefingStatus.Approved
                || b.Status == BriefingStatus.InProgress);

    /// <summary>
    /// The briefings marked late (BRF-043) that still need acting on. A closed briefing keeps its mark as the
    /// record of a late demand, but is not part of the operational list of RF030.
    /// </summary>
    private IQueryable<Briefing> Overdue() => Open().Where(b => b.OverdueSince != null);
}
