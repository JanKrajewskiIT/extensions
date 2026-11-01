using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Extensions.Jobs;

public class RecurringJobScheduler<TSchedule, TLease, TJob>(ScopedJobOperation<IRecurringJobRuntime<TSchedule, TLease, TJob>> operations,
	TimeProvider clock, ILogger<RecurringJobScheduler<TSchedule, TLease, TJob>> logger) : BackgroundService
	where TSchedule : class where TLease : class, IRecurringOccurrence where TJob : class
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

	private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(10);

	private RecurringScheduleCursor? after;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await RunPassAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception failure)
			{
				logger.LogError("Recurring job scheduler pass failed ({FailureType})", failure.GetType().Name);
			}

			if (!await BackgroundServiceDelay.WaitAsync(PollInterval, clock, stoppingToken))
			{
				break;
			}
		}
	}

	private async ValueTask RunPassAsync(CancellationToken token)
	{
		var page = await operations.RunAsync((runtime, operation) => runtime.ReadPageAsync(after, operation), token);
		after = page.Next;
		var definitions = await operations.RunAsync((runtime, _) => ValueTask.FromResult(page.Items.Select(value =>
			(Definition: value, Details: runtime.Describe(value))).OrderByDescending(value => value.Details.Priority)
				.ThenBy(value => value.Details.RequestedBy, StringComparer.Ordinal).ThenBy(value => value.Details.Id, StringComparer.Ordinal).ToArray()), token);
		foreach (var (definition, details) in definitions)
		{
			try
			{
				var count = details.MisfirePolicy == RecurringMisfirePolicy.CatchUp ? details.MaximumCatchUpRuns : 1;
				for (var occurrence = 0; occurrence < count; occurrence++)
				{
					if (!await DispatchDueAsync(definition, details, token))
					{
						break;
					}
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception failure)
			{
				logger.LogWarning("Recurring schedule occurrence failed ({FailureType}); other schedules will continue", failure.GetType().Name);
			}
		}
	}

	private async ValueTask<bool> DispatchDueAsync(TSchedule definition, RecurringScheduleInfo details, CancellationToken token)
	{
		var lease = await operations.RunAsync((runtime, operation) => runtime.AcquireAsync(definition, details.RequestedBy, operation), token);
		if (lease is null)
		{
			return false;
		}

		var committed = false;
		try
		{
			await using var preparation = new RecurringOccurrencePreparation(
				operation => operations.RunAsync((runtime, renewal) => runtime.RenewAsync(lease, renewal), operation), clock, lease, token);
			try
			{
				var missing = await operations.RunAsync((runtime, operation) =>
					runtime.MissingDependenciesAsync(definition, details.RequestedBy, lease.ScheduledAt, operation), preparation.Token);
				if (!missing.IsEmpty)
				{
					await preparation.StopRenewalAsync();
					await operations.RunAsync((runtime, operation) => runtime.RecordWaitingAsync(lease, missing, operation), preparation.Token);
					return false;
				}

				TJob? job = null;
				var missed = clock.GetUtcNow() - lease.ScheduledAt >= details.Interval;
				if (!(missed && details.MisfirePolicy == RecurringMisfirePolicy.Skip))
				{
					job = await operations.RunAsync((runtime, operation) => runtime.MaterializeAsync(definition, lease, operation), preparation.Token);
					preparation.ThrowIfInvalid();
				}

				await preparation.StopRenewalAsync();
				await operations.RunAsync((runtime, operation) => runtime.CommitAsync(lease, job, operation), preparation.Token);
				committed = true;
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
				await preparation.StopRenewalAsync();
				throw;
			}
		}
		finally
		{
			if (!committed)
			{
				using var timeout = new CancellationTokenSource(ReleaseTimeout, clock);
				try
				{
					await operations.RunAsync((runtime, operation) => runtime.ReleaseAsync(lease, operation), timeout.Token);
				}
				catch (Exception failure)
				{
					logger.LogWarning("Recurring preparation lease cleanup failed ({FailureType}); expiry will release ownership", failure.GetType().Name);
				}
			}
		}

		return committed;
	}
}
