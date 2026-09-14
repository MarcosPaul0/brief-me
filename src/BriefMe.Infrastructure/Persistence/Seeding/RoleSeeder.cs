using BriefMe.Domain.Constants;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Repositories;

namespace BriefMe.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the native roles and their permissions (USR-047). Runs after <see cref="PermissionSeeder"/>.
/// Idempotent by role <c>Name</c>: creates missing roles as system roles and adds any missing
/// permission associations, never removing existing ones. Fails clearly if a referenced permission
/// code is absent from the catalog.
/// </summary>
public sealed class RoleSeeder(
    IRoleRepository roles,
    IPermissionRepository permissions,
    IUnitOfWork unitOfWork) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var permissionIdByCode = (await permissions.GetAllAsync(cancellationToken))
            .ToDictionary(permission => permission.Code, permission => permission.Id, StringComparer.Ordinal);

        EnsureAllCodesExist(permissionIdByCode.Keys);

        var changed = false;

        foreach (var definition in Roles.Catalog)
        {
            var role = await GetTrackedRoleAsync(definition.Name, cancellationToken);

            if (role is null)
            {
                role = Role.Create(definition.Name, definition.Description, isSystemRole: true);
                await roles.AddAsync(role, cancellationToken);
                changed = true;
            }

            foreach (var code in definition.PermissionCodes)
            {
                var permissionId = permissionIdByCode[code];

                if (role.RolePermissions.Any(rolePermission => rolePermission.PermissionId == permissionId))
                    continue;

                role.AddPermission(permissionId);
                changed = true;
            }
        }

        if (changed)
            await unitOfWork.CommitAsync(cancellationToken);
    }

    // Loads the role with its permissions so re-runs dedupe against what is already persisted.
    private async Task<Role?> GetTrackedRoleAsync(string name, CancellationToken cancellationToken)
    {
        var existing = await roles.GetByNameAsync(name, cancellationToken);

        return existing is null
            ? null
            : await roles.GetByIdWithPermissionsAsync(existing.Id, cancellationToken);
    }

    private static void EnsureAllCodesExist(IReadOnlyCollection<string> availableCodes)
    {
        var available = availableCodes.ToHashSet(StringComparer.Ordinal);

        var missing = Roles.Catalog
            .SelectMany(definition => definition.PermissionCodes)
            .Distinct(StringComparer.Ordinal)
            .Where(code => !available.Contains(code))
            .ToList();

        if (missing.Count > 0)
            throw new DomainException(
                $"Permissions ausentes no catálogo para o seed de roles: {string.Join(", ", missing)}.");
    }
}
