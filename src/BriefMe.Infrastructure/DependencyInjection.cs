using BriefMe.Application.Abstractions;
using BriefMe.Domain.Repositories;
using BriefMe.Infrastructure.Auth;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using BriefMe.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Minio;

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
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IBriefingRepository, BriefingRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddObjectStorage(configuration);

        // Startup seeders. Registration order defines execution order in MigrateAndSeedAsync.
        services.AddScoped<IDataSeeder, AdminUserSeeder>();
        services.AddScoped<IDataSeeder, CategorySeeder>();

        return services;
    }

    private static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ObjectStorageSettings.SectionName);
        services.Configure<ObjectStorageSettings>(section);

        var settings = section.Get<ObjectStorageSettings>() ?? new ObjectStorageSettings();

        // Built here instead of via AddMinio(), which calls services.BuildServiceProvider() during
        // registration and would leave a second container behind. Building the client opens no connection,
        // so the app boots even when MinIO is down.
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(settings.Endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey)
            .WithSSL(settings.UseSsl)
            .Build());
        services.AddSingleton<IFileStorage, MinioFileStorage>();

        services.Configure<AttachmentLinkSettings>(configuration.GetSection(AttachmentLinkSettings.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAttachmentLinkSigner, HmacAttachmentLinkSigner>();

        return services;
    }
}
