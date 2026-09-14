using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Domain.Repositories;
using BriefMe.Infrastructure.Auth;
using BriefMe.Infrastructure.Messaging;
using BriefMe.Infrastructure.Notifications;
using BriefMe.Infrastructure.Sla;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using BriefMe.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Minio;

namespace BriefMe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        // The system clock, injectable so "is this deadline past?" (BRF-043) can be tested without waiting.
        services.AddSingleton(TimeProvider.System);

        // Delivers what aggregates raised, after the commit (BRF-043). UnitOfWork is what calls it, which is
        // what makes every use case publish without knowing it does (BRF-049).
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.Configure<PasswordHasherSettings>(configuration.GetSection(PasswordHasherSettings.SectionName));
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtService, JwtService>();

        services.Configure<RefreshTokenSettings>(configuration.GetSection(RefreshTokenSettings.SectionName));
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
        services.AddSingleton<IFileValidator, FileValidator>();
        services.AddFileStorage(configuration);

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IBriefingRepository, BriefingRepository>();
        services.AddScoped<IBriefingTaskRepository, BriefingTaskRepository>();
        services.AddScoped<ISlaRuleRepository, SlaRuleRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        // Placeholder category catalog (BRF-013) — immutable seed data, no per-request state, so a singleton.
        // BRF-059 swaps this for the EF-backed Category aggregate without touching the Domain interface.
        services.AddSingleton<ICategoryRepository, SeededCategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // In-app inbox delivery (RF031–RF035, BRF-048). Scoped, like the repository and unit of work it
        // writes through, so it works both inside a request and inside the scope the SLA job opens for
        // itself. Nothing calls it yet — its consumers are the notification handlers of BRF-050–053.
        services.AddScoped<INotificationService, InAppNotificationService>();

        // Deadline monitor (RF028, BRF-043). Registered unconditionally; SlaMonitor:Enabled decides at run
        // time whether it sweeps, so turning it off does not change the shape of the container.
        services.Configure<SlaMonitorSettings>(configuration.GetSection(SlaMonitorSettings.SectionName));
        services.AddHostedService<SlaMonitorBackgroundService>();

        // Startup seeders (USR-046+). Registration order defines execution order in MigrateAndSeedAsync.
        services.AddScoped<IDataSeeder, PermissionSeeder>();
        services.AddScoped<IDataSeeder, RoleSeeder>();
        services.AddScoped<IDataSeeder, AdminUserSeeder>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IFileStorageService"/> implementation named by <c>FileStorage:Provider</c>
    /// (BRF-011). Only the selected provider is registered, so an unconfigured MinIO cannot be reached by
    /// accident and the local provider needs no bucket.
    /// </summary>
    private static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(FileStorageSettings.SectionName);
        var provider = section.GetValue(nameof(FileStorageSettings.Provider), FileStorageProvider.Local);

        if (provider is not FileStorageProvider.Minio)
        {
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            return services;
        }

        services.Configure<MinioSettings>(configuration.GetSection(MinioSettings.SectionName));

        var minio = configuration.GetSection(MinioSettings.SectionName).Get<MinioSettings>() ?? new MinioSettings();

        // Built here instead of via AddMinio(), which calls services.BuildServiceProvider() during
        // registration and would leave a second container behind. Singleton: the client owns an HttpClient
        // that a shorter lifetime disposes mid-upload (minio-dotnet#1369).
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(minio.Endpoint)
            .WithCredentials(minio.AccessKey, minio.SecretKey)
            .WithSSL(minio.UseSsl)
            .Build());

        services.AddSingleton<IFileStorageService, MinioFileStorageService>();

        return services;
    }
}
