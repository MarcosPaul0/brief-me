using System.Reflection;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Behaviors;
using BriefMe.Application.Messaging;
using BriefMe.Domain.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BriefMe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<ISender, Sender>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Stateless, dependency-free calculations — one instance serves every request.
        services.AddSingleton<BriefingProgressCalculator>();  // BRF-035
        services.AddSingleton<SlaDeadlineResolver>();         // BRF-043

        RegisterHandlers(services, assembly, typeof(IRequestHandler<,>));
        RegisterHandlers(services, assembly, typeof(IDomainEventHandler<>));

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }

    /// <summary>
    /// Registers every implementation of <paramref name="openHandlerType"/> found in
    /// <paramref name="assembly"/> against the closed interface it implements.
    /// </summary>
    /// <remarks>
    /// Uses <c>AddScoped</c> rather than <c>TryAdd</c> for both axes: a request has exactly one handler, so a
    /// duplicate is a bug that should surface as an ambiguity, and a domain event may legitimately have
    /// several — the dispatcher resolves them all (BRF-049).
    /// </remarks>
    private static void RegisterHandlers(IServiceCollection services, Assembly assembly, Type openHandlerType)
    {
        var registrations =
            from implementationType in assembly.GetTypes()
            where implementationType is { IsAbstract: false, IsInterface: false }
            from serviceType in implementationType.GetInterfaces()
            where serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == openHandlerType
            select (ServiceType: serviceType, ImplementationType: implementationType);

        foreach (var (serviceType, implementationType) in registrations)
            services.AddScoped(serviceType, implementationType);
    }
}
