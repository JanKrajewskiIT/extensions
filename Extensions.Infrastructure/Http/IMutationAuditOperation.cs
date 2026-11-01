namespace Extensions.Infrastructure.Http;

/// <summary>Owned mutation lifetime. A critical operation can supply a durable receipt execution strategy.</summary>
public interface IMutationAuditOperation
{
	/// <summary>Runs and fully awaits the mutation, without detaching work on timeout.</summary>
	ValueTask<object?> ExecuteAsync(Func<CancellationToken, ValueTask<object?>> action, CancellationToken cancellationToken);
}
