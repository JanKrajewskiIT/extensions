using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Http;

/// <summary>Noncritical HTTP outcome audit; secondary failures never replace the primary mutation result.</summary>
public sealed class ObservedMutationAuditOperation : IMutationAuditOperation
{
	private readonly Guid operationId;
	private readonly Func<IDisposable> enterScope;
	private readonly Func<MutationAuditOutcome, CancellationToken, ValueTask> record;
	private readonly TimeProvider clock;
	private readonly ILogger<ObservedMutationAuditOperation> logger;

	/// <summary>Creates an observer; the ambient scope is entered only while executing the mutation.</summary>
	public ObservedMutationAuditOperation(Guid operationId, Func<IDisposable> enterScope,
		Func<MutationAuditOutcome, CancellationToken, ValueTask> record, TimeProvider clock,
		ILogger<ObservedMutationAuditOperation> logger)
	{
		if (operationId == Guid.Empty)
		{
			throw new ArgumentException("A nonempty operation identity is required.", nameof(operationId));
		}

		ArgumentNullException.ThrowIfNull(enterScope);
		ArgumentNullException.ThrowIfNull(record);
		ArgumentNullException.ThrowIfNull(clock);
		ArgumentNullException.ThrowIfNull(logger);
		this.operationId = operationId;
		this.enterScope = enterScope;
		this.record = record;
		this.clock = clock;
		this.logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask<object?> ExecuteAsync(Func<CancellationToken, ValueTask<object?>> action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		using var scope = enterScope();
		object? result;
		try
		{
			result = await action(cancellationToken);
		}
		catch
		{
			await RecordAsync(MutationAuditOutcome.Failed);
			throw;
		}

		await RecordAsync(result is IStatusCodeHttpResult { StatusCode: >= 400 } ? MutationAuditOutcome.Denied : MutationAuditOutcome.Succeeded);
		return result;
	}

	private async ValueTask RecordAsync(MutationAuditOutcome outcome)
	{
		try
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
			await record(outcome, deadline.Token);
		}
		catch (Exception secondary)
		{
			logger.LogError("API result audit failed for {OperationId} ({FailureType}); committed mutation evidence is retained",
				operationId, secondary.GetType().Name);
		}
	}
}
