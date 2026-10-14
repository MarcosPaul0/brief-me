using BriefMe.Domain.Entities;
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
