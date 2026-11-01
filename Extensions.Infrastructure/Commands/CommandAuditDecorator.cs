using Extensions.Application.Commands;
using Extensions.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Commands;

internal sealed class CommandAuditDecorator<TContext>(TContext context, TimeProvider clock) where TContext : DbContext
{
    public async Task<TResponse> ExecuteAsync<TResponse>(Guid receiptId, CommandContext invocation, ContentHash requestHash,
        Func<CancellationToken, Task<TResponse>> next, CancellationToken token)
    {
        var result = await next(token);
        context.Set<CommandAuditEntry>().Add(new CommandAuditEntry
        {
            Id = receiptId,
            CommandId = invocation.CommandId,
            Owner = invocation.Owner,
            Actor = invocation.Actor,
            Partition = invocation.Partition,
            Operation = invocation.Operation,
            RequestHash = requestHash.Sha256,
            CorrelationId = invocation.Trace?.CorrelationId,
            CausationId = invocation.Trace?.CausationId,
            RecordedAt = clock.GetUtcNow(),
        });
        return result;
    }
}
