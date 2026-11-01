using Extensions.Domain.Primitives;

namespace Extensions.Jobs;

public class LocalWorkBudget(int maximumAttempts, CancellationToken token, Func<string, Exception> failure) : IExecutionBudget
{
	private int remaining = maximumAttempts > 0
		? maximumAttempts
		: throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

	public bool OwnsComputeReservation => false;

	public CancellationToken Token => token;

	public void Track(Task pending)
	{
		ArgumentNullException.ThrowIfNull(pending);
	}

	public ValueTask TakeAsync(WorkAttemptKind kind, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		DomainGuard.Defined(kind, nameof(kind));
		if (Interlocked.Decrement(ref remaining) < 0)
		{
			throw failure("budget.operation_attempts_exhausted");
		}

		return ValueTask.CompletedTask;
	}
}
