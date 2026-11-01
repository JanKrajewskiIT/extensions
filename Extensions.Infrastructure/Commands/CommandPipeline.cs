using System.Text;
using Extensions.Application.Commands;
using Extensions.Domain.Exceptions;
using Extensions.Domain.Primitives;
using Extensions.Domain.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Commands;

internal sealed class CommandPipeline<TContext, TCommand, TResponse>(
    ICommandContextProvider<TCommand> contexts, CommandValidationDecorator<TCommand> validation,
    EfCommandTransaction<TContext> transactions, CommandPipelineSettings settings,
    Func<IServiceProvider, TContext, TCommand, CommandContext, ContentHash, CancellationToken, Task<TResponse>> attempt)
    where TContext : DbContext
{
    public async Task<TResponse> ExecuteAsync(TCommand command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var json = PayloadSerializer.Serialize(command);
        if (Encoding.UTF8.GetByteCount(json) > settings.MaximumRequestBytes)
        {
            throw new RequestValidationException("command.payload_too_large");
        }

        var snapshot = PayloadSerializer.Deserialize<TCommand>(json);
        var context = contexts.Create(snapshot);
        await validation.ValidateAsync(snapshot, token);
        var hash = PayloadSerializer.Hash(json);
        return await transactions.ExecuteAsync((provider, database, cancellation) =>
            attempt(provider, database, PayloadSerializer.Deserialize<TCommand>(json), context, hash, cancellation), token);
    }
}
