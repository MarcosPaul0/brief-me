namespace BriefMe.Domain.Repositories;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
