using System.Text;
using System.Text.Json;
using Extensions.Application.Commands;
using Extensions.Domain.Exceptions;
using Extensions.Domain.Primitives;
using Extensions.Domain.Serialization;
using Extensions.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Commands;

internal sealed class CommandIdempotencyDecorator<TContext>(TContext context, TimeProvider clock,
    CommandPipelineSettings settings) where TContext : DbContext
{
    public async Task<TResponse> ExecuteAsync<TResponse>(CommandContext invocation, ContentHash requestHash,
        Func<Guid, CancellationToken, Task<TResponse>> next, CancellationToken token)
    {
        var id = ContentIdentity.Create("extensions.command-receipt", invocation.Owner, invocation.Partition,
            invocation.Operation, invocation.CommandId.ToString("N"));
        var lockKey = DatabaseLockKeys.Create("extensions.command", id.ToString("N"));
        // ReadCommitted refreshes the receipt read after another transaction releases this lock.
        await context.LockAsync(DatabaseLockKeys.FromText(lockKey), token);
        var receipt = await context.Set<CommandReceipt>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, token);
        if (receipt is not null)
        {
            if (receipt.Owner != invocation.Owner || receipt.Partition != invocation.Partition ||
                receipt.Operation != invocation.Operation || receipt.CommandId != invocation.CommandId)
            {
                throw new DataInconsistencyException("command.receipt_identity_invalid", "The command receipt identity is invalid.", null);
            }

            if (receipt.RequestHash != requestHash.Sha256)
            {
                throw new ConcurrencyConflictException("command.identity_reused");
            }

            var hasAudit = await context.Set<CommandAuditEntry>().AsNoTracking().AnyAsync(row => row.Id == id &&
                row.CommandId == invocation.CommandId && row.Owner == invocation.Owner && row.Partition == invocation.Partition &&
                row.Operation == invocation.Operation && row.RequestHash == requestHash.Sha256, token);
            if (!hasAudit)
            {
                throw InvalidReceipt();
            }

            return ReadResult<TResponse>(receipt);
        }

        var response = await next(id, token);
        var json = PayloadSerializer.Serialize(response);
        if (Encoding.UTF8.GetByteCount(json) > settings.MaximumResponseBytes)
        {
            throw new RequestValidationException("command.result_too_large");
        }

        context.Set<CommandReceipt>().Add(new CommandReceipt
        {
            Id = id,
            CommandId = invocation.CommandId,
            Owner = invocation.Owner,
            Partition = invocation.Partition,
            Operation = invocation.Operation,
            RequestHash = requestHash.Sha256,
            ResultJson = json,
            ResultHash = PayloadSerializer.Hash(json).Sha256,
            CompletedAt = clock.GetUtcNow(),
        });
        return response;
    }

    private TResponse ReadResult<TResponse>(CommandReceipt receipt)
    {
        if (Encoding.UTF8.GetByteCount(receipt.ResultJson) > settings.MaximumResponseBytes)
        {
            throw InvalidReceipt();
        }

        try
        {
            if (PayloadSerializer.Hash(receipt.ResultJson).Sha256 != receipt.ResultHash)
            {
                throw InvalidReceipt();
            }

            // A handler may intentionally return null; the canonical document decoder rejects it.
            return JsonSerializer.Deserialize<TResponse>(receipt.ResultJson, PayloadSerializer.Options)!;
        }
        catch (JsonException failure)
        {
            throw InvalidReceipt(failure);
        }
    }

    private static DataInconsistencyException InvalidReceipt(Exception? cause = null) =>
        new("command.receipt_integrity_failed", "The retained command outcome is incomplete or damaged.", cause);
}
