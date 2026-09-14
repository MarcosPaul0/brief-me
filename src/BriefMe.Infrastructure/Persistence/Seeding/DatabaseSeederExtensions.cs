using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BriefMe.Infrastructure.Persistence.Seeding;

public static class DatabaseSeederExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations and runs every registered <see cref="IDataSeeder"/> in
    /// registration order, inside a dedicated scope. Intended for controlled environments (USR-046).
    /// </summary>
    public static async Task MigrateAndSeedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>())
            await seeder.SeedAsync(cancellationToken);
    }
}
