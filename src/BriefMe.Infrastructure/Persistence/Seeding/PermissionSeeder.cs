using BriefMe.Domain.Constants;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the permission catalog (USR-046). Idempotent by <c>Code</c>: inserts only the definitions
/// that are not yet persisted and never updates or removes existing rows.
/// </summary>
public sealed class PermissionSeeder(IPermissionRepository permissions, IUnitOfWork unitOfWork) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingCodes = (await permissions.GetAllAsync(cancellationToken))
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Permissions.Catalog
            .Where(definition => !existingCodes.Contains(definition.Code))
            .ToList();

        if (missing.Count == 0)
            return;

        foreach (var definition in missing)
        {
            var permission = Permission.Create(definition.Code, definition.Name, definition.Description, definition.Group);
            await permissions.AddAsync(permission, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);
    }
}
