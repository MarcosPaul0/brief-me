using BriefMe.Application.Abstractions;
using BriefMe.Domain.Constants;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the initial administrator user (USR-048) so a freshly provisioned system is accessible.
/// Runs after <see cref="RoleSeeder"/> (needs the Administrador role). Idempotent by e-mail: never
/// recreates the user if it already exists. The password is BCrypt-hashed via
/// <see cref="IPasswordHasher"/> — the plaintext default is only used to derive the hash.
/// </summary>
/// <remarks>
/// SECURITY: the default credentials below are a bootstrap convenience for controlled environments.
/// The default password MUST be changed after the first login in production. Forcing a password
/// change on first access (a dedicated flag on <see cref="User"/>) is recommended but out of scope
/// for this seed.
/// </remarks>
public sealed class AdminUserSeeder(
    IUserRepository users,
    IRoleRepository roles,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IDataSeeder
{
    private const string AdminFullName = "Administrador";
    private const string AdminEmail = "admin@empresa.com";
    private const string DefaultPassword = "Admin@123";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = Email.Create(AdminEmail).Value;

        if (await users.EmailExistsAsync(email, cancellationToken: cancellationToken))
            return;

        var adminRole = await roles.GetByNameAsync(Roles.Administrador, cancellationToken)
            ?? throw new DomainException(
                $"Role '{Roles.Administrador}' não encontrada. Execute o seed de roles antes do seed do administrador.");

        var passwordHash = passwordHasher.Hash(DefaultPassword);

        var admin = User.Create(AdminFullName, email, passwordHash);
        admin.AssignRole(adminRole.Id);

        await users.AddAsync(admin, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
