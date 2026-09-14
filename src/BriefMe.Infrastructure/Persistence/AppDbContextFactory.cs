using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BriefMe.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by the EF Core tools (migrations add/remove/script). It builds the
/// <see cref="AppDbContext"/> directly, bypassing the Web host's runtime DI — which cannot be constructed
/// until every use-case dependency is registered (e.g. <c>ICategoryRepository</c>, delivered by BRF-059).
/// Never used at application runtime. The connection string only needs to be a valid Npgsql string;
/// <c>migrations add</c> does not open a database connection.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=briefme;Username=briefme;Password=briefme";

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DesignTimeConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
