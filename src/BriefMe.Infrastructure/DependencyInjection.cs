using BriefMe.Application.Abstractions;
using BriefMe.Domain.Repositories;
using BriefMe.Infrastructure.Auth;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.Configure<PasswordHasherSettings>(configuration.GetSection(PasswordHasherSettings.SectionName));
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtService, JwtService>();

        services.Configure<RefreshTokenSettings>(configuration.GetSection(RefreshTokenSettings.SectionName));
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IBriefingRepository, BriefingRepository>();
        // Placeholder category catalog (BRF-013) — immutable seed data, no per-request state, so a singleton.
        // BRF-059 swaps this for the EF-backed Category aggregate without touching the Domain interface.
        services.AddSingleton<ICategoryRepository, SeededCategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Startup seeders (USR-046+). Registration order defines execution order in MigrateAndSeedAsync.
        services.AddScoped<IDataSeeder, PermissionSeeder>();
        services.AddScoped<IDataSeeder, RoleSeeder>();
        services.AddScoped<IDataSeeder, AdminUserSeeder>();

        return services;
    }
}
