using System.Reflection;
using Extensions.Application.Abstractions;
using Extensions.Infrastructure.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Extensions.Infrastructure;

/// <summary>Opt-in command decorators; the existing nongeneric registration keeps its original behavior.</summary>
public static class CommandPipelineRegistrationExtensions
{
    /// <summary>
    /// Registers validation, fresh-scope PostgreSQL transactions/retries, durable idempotency and local audit.
    /// The host supplies each command's trusted context provider and opts its own DbContext into AddCommandPipeline.
    /// Queries remain unchanged. Handlers must write only through TContext and use an outbox for external effects.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly<TContext>(this IServiceCollection services,
        Assembly assembly, Action<CommandPipelineOptions>? configure = null) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        var options = new CommandPipelineOptions();
        configure?.Invoke(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumRequestBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumResponseBytes);
        var settings = new CommandPipelineSettings(options.MaximumRequestBytes, options.MaximumResponseBytes);
        var first = services.Count;
        services.AddHandlersFromAssembly(assembly);
        var last = services.Count;
        for (var index = first; index < last; index++)
        {
            var descriptor = services[index];
            if (!descriptor.ServiceType.IsGenericType || descriptor.ImplementationType is not { } handlerType)
            {
                continue;
            }

            var contract = descriptor.ServiceType.GetGenericTypeDefinition();
            if (contract == typeof(ICommandHandler<>) || contract == typeof(ICommandHandler<,>))
            {
                services[index] = ServiceDescriptor.Scoped(descriptor.ServiceType,
                    CommandHandlerFactory.Create(typeof(TContext), descriptor.ServiceType, handlerType, settings));
            }
        }

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        return services;
    }
}
