using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public sealed class RecurringOccurrencePreparation : IAsyncDisposable
{
	private readonly Func<CancellationToken, ValueTask> renew;

	private readonly TimeProvider clock;

	private readonly IRecurringOccurrence lease;

	private readonly CancellationToken stoppingToken;

	private readonly CancellationTokenSource deadlineCancellation;

	private readonly CancellationTokenSource workCancellation;

	private readonly CancellationTokenSource renewalCancellation;

	private readonly Task renewals;

	private Exception? renewalFailure;

	public RecurringOccurrencePreparation(Func<CancellationToken, ValueTask> renew, TimeProvider clock,
		IRecurringOccurrence lease, CancellationToken stoppingToken)
	{
		this.renew = renew;
		this.clock = clock;
		this.lease = lease;
		this.stoppingToken = stoppingToken;
		var remaining = lease.PreparationDeadline - clock.GetUtcNow();
		deadlineCancellation = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, clock);
		workCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadlineCancellation.Token);
		renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(workCancellation.Token);
		renewals = RenewAsync();
	}

	public CancellationToken Token => workCancellation.Token;

	public async ValueTask StopRenewalAsync()
	{
		await renewalCancellation.CancelAsync();
		await renewals;
		ThrowIfInvalid();
	}

	public void ThrowIfInvalid()
	{
		stoppingToken.ThrowIfCancellationRequested();
		if (renewalFailure is not null)
		{
			throw new DbUpdateConcurrencyException("The recurring preparation could not retain its lease.", renewalFailure);
		}

		if (deadlineCancellation.IsCancellationRequested || clock.GetUtcNow() >= lease.PreparationDeadline)
		{
			throw new TimeoutException("The recurring occurrence exhausted its preparation and execution window.");
		}

		Token.ThrowIfCancellationRequested();
	}

	public async ValueTask DisposeAsync()
	{
		await renewalCancellation.CancelAsync();
		await renewals;
		renewalCancellation.Dispose();
		workCancellation.Dispose();
		deadlineCancellation.Dispose();
	}

	private async Task RenewAsync()
	{
		try
		{
			while (true)
			{
				await Task.Delay(RecurringScheduleTiming.RenewalInterval, clock, renewalCancellation.Token);
				using var timeout = new CancellationTokenSource(RecurringScheduleTiming.RenewalInterval, clock);
				using var operation = CancellationTokenSource.CreateLinkedTokenSource(renewalCancellation.Token, timeout.Token);
				await renew(operation.Token);
			}
		}
		catch (OperationCanceledException) when (renewalCancellation.IsCancellationRequested)
		{
			return;
		}
		catch (Exception failure)
		{
			renewalFailure = failure;
			await workCancellation.CancelAsync();
		}
	}
}
