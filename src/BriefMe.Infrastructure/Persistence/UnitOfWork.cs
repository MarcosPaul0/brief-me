using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The record was changed by someone else since it was loaded.", exception);
        }
    }
}
