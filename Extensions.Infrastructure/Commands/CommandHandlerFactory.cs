using System.Reflection;
using System.Runtime.ExceptionServices;
using Extensions.Application.Abstractions;
using Extensions.Application.Commands;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Commands;

internal static class CommandHandlerFactory
{
    internal static Func<IServiceProvider, object> Create(Type contextType, Type serviceType, Type handlerType,
        CommandPipelineSettings settings)
    {
        var arguments = serviceType.GetGenericArguments();
        var methodName = serviceType.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
            ? nameof(CreateCommand) : nameof(CreateResponseCommand);
        var method = typeof(CommandHandlerFactory).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod([contextType, .. arguments]);
        return provider =>
        {
            try
            {
                return method.Invoke(null, [provider, handlerType, settings])
                    ?? throw new InvalidOperationException("The command pipeline factory returned no handler.");
            }
            catch (TargetInvocationException failure) when (failure.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        };
    }

    private static object CreateCommand<TContext, TCommand>(IServiceProvider provider, Type handlerType,
        CommandPipelineSettings settings) where TContext : DbContext where TCommand : ICommand
    {
        var pipeline = CreatePipeline<TContext, TCommand, CommandUnit>(provider, settings,
            async (attempt, command, token) =>
            {
                await ((ICommandHandler<TCommand>)attempt.GetRequiredService(handlerType)).ExecuteAsync(command, token);
                return default;
            });
        return new PipelineCommandHandler<TContext, TCommand>(pipeline);
    }

    private static object CreateResponseCommand<TContext, TCommand, TResponse>(IServiceProvider provider, Type handlerType,
        CommandPipelineSettings settings) where TContext : DbContext where TCommand : ICommand<TResponse>
    {
        var pipeline = CreatePipeline<TContext, TCommand, TResponse>(provider, settings,
            (attempt, command, token) =>
                ((ICommandHandler<TCommand, TResponse>)attempt.GetRequiredService(handlerType)).ExecuteAsync(command, token));
        return new PipelineResponseCommandHandler<TContext, TCommand, TResponse>(pipeline);
    }

    private static CommandPipeline<TContext, TCommand, TResponse> CreatePipeline<TContext, TCommand, TResponse>(
        IServiceProvider provider, CommandPipelineSettings settings,
        Func<IServiceProvider, TCommand, CancellationToken, Task<TResponse>> handler) where TContext : DbContext
    {
        var transactions = new EfCommandTransaction<TContext>(provider.GetRequiredService<TContext>(),
            provider.GetRequiredService<IServiceScopeFactory>(), attempt => attempt.GetRequiredService<TContext>(),
            provider.GetRequiredService<ILogger<EfCommandTransaction<TContext>>>());
        return new(provider.GetRequiredService<ICommandContextProvider<TCommand>>(),
            new CommandValidationDecorator<TCommand>(provider.GetServices<IValidator<TCommand>>()),
            transactions, settings, async (attempt, database, command, captured, hash, token) =>
            {
                foreach (var initializer in attempt.GetServices<ICommandAttemptInitializer>())
                {
                    initializer.Initialize(captured);
                }

                var current = attempt.GetRequiredService<ICommandContextProvider<TCommand>>().Create(command);
                if (current != captured)
                {
                    throw new UnauthorizedAccessException("Command authority changed between invocation and its transaction attempt.");
                }

                var clock = attempt.GetRequiredService<TimeProvider>();
                var audit = new CommandAuditDecorator<TContext>(database, clock);
                var idempotency = new CommandIdempotencyDecorator<TContext>(database, clock, settings);
                return await idempotency.ExecuteAsync(captured, hash, (id, cancellation) =>
                    audit.ExecuteAsync(id, captured, hash, next => handler(attempt, command, next), cancellation), token);
            });
    }
}
