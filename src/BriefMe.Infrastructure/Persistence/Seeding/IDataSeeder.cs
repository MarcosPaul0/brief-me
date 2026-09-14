namespace BriefMe.Infrastructure.Persistence.Seeding;

/// <summary>
/// A single idempotent seeding step. Implementations are discovered via DI and run in registration
/// order at startup (USR-046 and later seeds). Running twice must be a no-op.
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
