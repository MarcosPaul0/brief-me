using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class SlaRuleRepository(AppDbContext dbContext) : EfRepository<SlaRule>(dbContext), ISlaRuleRepository
{
    public async Task<SlaRule?> GetByScopeAsync(
        Guid? categoryId,
        Priority? priority,
        CancellationToken cancellationToken = default)
    {
        // Each side is branched instead of compared to a nullable parameter: `Column == parameter` leaves the
        // NULL case to the provider's null-semantics rewriting, while `Column == null` puts `IS NULL` in the
        // SQL by construction. It matters most for the global rule, whose scope is NULL on both sides.
        var query = DbContext.SlaRules.AsQueryable();

        query = categoryId is null
            ? query.Where(rule => rule.CategoryId == null)
            : query.Where(rule => rule.CategoryId == categoryId);

        query = priority is null
            ? query.Where(rule => rule.Priority == null)
            : query.Where(rule => rule.Priority == priority);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SlaRule>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.SlaRules.ToListAsync(cancellationToken);
}
