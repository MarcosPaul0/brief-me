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

    public async Task<IReadOnlyList<Briefing>> GetQueueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await Queue()
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountQueueAsync(CancellationToken cancellationToken = default) =>
        await Queue().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Briefing>> GetByDesignerAsync(
        Guid designerId,
        BriefingStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await ByDesigner(designerId, status)
            .OrderBy(b => b.DesiredDeliveryDate == null)
            .ThenBy(b => b.DesiredDeliveryDate)
            .ThenBy(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByDesignerAsync(
        Guid designerId,
        BriefingStatus? status,
        CancellationToken cancellationToken = default) =>
        await ByDesigner(designerId, status).CountAsync(cancellationToken);

    private IQueryable<Briefing> Queue() =>
        DbContext.Briefings.Where(b => b.Status == BriefingStatus.Submitted && b.DesignerId == null);

    private IQueryable<Briefing> ByDesigner(Guid designerId, BriefingStatus? status)
    {
        var query = DbContext.Briefings.Where(b => b.DesignerId == designerId);

        if (status is not null)
            query = query.Where(b => b.Status == status);

        return query;
    }

    private IQueryable<Briefing> ByRequester(Guid requesterId, BriefingFilter filter)
    {
        var query = DbContext.Briefings.Where(b => b.RequesterId == requesterId);

        if (filter.Status is not null)
            query = query.Where(b => b.Status == filter.Status);

        if (filter.CategoryId is not null)
            query = query.Where(b => b.CategoryId == filter.CategoryId);

        return query;
    }
}
