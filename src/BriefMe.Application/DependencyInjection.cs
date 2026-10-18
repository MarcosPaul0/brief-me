using System.Reflection;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Behaviors;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Messaging;
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
        services.AddScoped<BriefingComplementsLoader>();
        services.AddScoped<BriefingAttachmentStager>();

        RegisterHandlers(services, assembly, typeof(IRequestHandler<,>));

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }

    /// <summary>
    /// Registers every implementation of <paramref name="openHandlerType"/> found in
    /// <paramref name="assembly"/> against the closed interface it implements.
    /// </summary>
    /// <remarks>
    /// Uses <c>AddScoped</c> rather than <c>TryAdd</c>: a request has exactly one handler, so a duplicate is a
    /// bug that should surface as an ambiguity.
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
