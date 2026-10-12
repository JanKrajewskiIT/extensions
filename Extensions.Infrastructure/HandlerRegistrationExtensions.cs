using System.Reflection;
using Extensions.Application.Abstractions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Infrastructure;

/// <summary>
/// Convention-based registration for CQRS handlers and FluentValidation validators.
/// Scans the given assembly for concrete types implementing ICommandHandler or IQueryHandler
/// and registers them as scoped services.
/// </summary>
public static class HandlerRegistrationExtensions
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    /// <summary>
    /// Scans <paramref name="assembly"/> for all concrete types that implement any CQRS handler
    /// interface and registers them as scoped services (both by concrete type and by interface).
    /// Also registers all FluentValidation validators.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.GetInterfaces().Any(IsHandlerInterface));

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);

            foreach (var iface in handlerType.GetInterfaces().Where(IsHandlerInterface))
            {
                services.AddScoped(iface, handlerType);
            }
        }

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        return services;
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());
}
