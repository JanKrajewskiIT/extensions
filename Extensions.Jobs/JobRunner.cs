using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Extensions.Jobs;

public class JobRunner<TJob>(IServiceScopeFactory scopes, TimeProvider clock, IOptions<JobRunnerOptions> options,
	ILogger<JobRunner<TJob>> logger, IJobCodec<TJob> codec, ScopedJobOperation<IJobPersistence<TJob>> persistence) : BackgroundService
	where TJob : class
{
	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		return Task.WhenAll(Enumerable.Range(0, options.Value.Parallelism).Select(_ => RunLoopAsync(stoppingToken)));
	}

	private async Task RunLoopAsync(CancellationToken stoppingToken)
	{
		var delay = TimeSpan.Zero;
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, clock, stoppingToken);
				}

				delay = TimeSpan.Zero;
				TJob? job;
				await using (var scope = scopes.CreateAsyncScope())
				{
					job = await scope.ServiceProvider.GetRequiredService<IJobWorker<TJob>>().ClaimAsync(options.Value, stoppingToken);
				}

				if (job is null)
				{
					delay = options.Value.PollInterval;
					continue;
				}

				await ExecuteJobAsync(job, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception failure)
			{
				logger.LogError("Durable job runner iteration failed ({FailureType})", failure.GetType().Name);
				delay = options.Value.PollInterval;
			}
		}
	}

	private async Task ExecuteJobAsync(TJob initial, CancellationToken stoppingToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var worker = scope.ServiceProvider.GetRequiredService<IJobWorker<TJob>>();
		using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
		var remaining = codec.Describe(initial).Definition.Deadline - clock.GetUtcNow();
		if (remaining <= TimeSpan.Zero)
		{
			executionCancellation.Cancel();
		}
		else if (remaining <= TimeSpan.FromDays(30))
		{
			executionCancellation.CancelAfter(remaining);
		}

		using var actor = worker.EnterScope(initial, options.Value.WorkerId);
		using var execution = new JobExecution<TJob>(initial, persistence, codec, clock, options.Value.LeaseDuration, executionCancellation);
		await ExecuteAttemptAsync(worker, execution, executionCancellation, stoppingToken);
	}

	private async Task ExecuteAttemptAsync(IJobWorker<TJob> worker, JobExecution<TJob> execution,
		CancellationTokenSource executionCancellation, CancellationToken stoppingToken)
	{
		using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
		var heartbeat = RenewLoopAsync(execution, executionCancellation, heartbeatCancellation.Token);
		var id = execution.Details.Definition.Id;
		async Task StopHeartbeatAsync()
		{
			try
			{
				await heartbeatCancellation.CancelAsync();
				await heartbeat;
			}
			catch (OperationCanceledException) when (heartbeatCancellation.IsCancellationRequested)
			{
				logger.LogDebug("Stopped heartbeat for job {JobId}", id);
			}
			catch (Exception secondary)
			{
				logger.LogWarning("Heartbeat cleanup failed for {JobId} ({FailureType}); the job outcome is retained", id, secondary.GetType().Name);
			}
		}

		try
		{
			var result = await worker.ExecuteAsync(execution, executionCancellation.Token);
			await StopHeartbeatAsync();
			executionCancellation.Token.ThrowIfCancellationRequested();
			await execution.FinishAsync(job => job.Complete(result, clock.GetUtcNow()), stoppingToken);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException) when (execution.Details.CancellationRequested)
		{
			await StopHeartbeatAsync();
			await execution.FinishAsync(job => job.AcknowledgeCancellation(clock.GetUtcNow()), stoppingToken);
		}
		catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Job {JobId} stopped after loss of its renewable lease", id);
		}
		catch (Exception failure)
		{
			await StopHeartbeatAsync();
			if (execution.Details.CancellationRequested)
			{
				await execution.FinishAsync(job => job.AcknowledgeCancellation(clock.GetUtcNow()), stoppingToken);
				return;
			}

			executionCancellation.Token.ThrowIfCancellationRequested();
			var classification = worker.Classify(failure, execution.Details.Attempts);
			if (classification.Deferred && classification.Kind != JobFailureKind.Transient ||
				classification.RetryAfter is { } retryAfter && retryAfter <= TimeSpan.Zero)
			{
				throw new InvalidOperationException("Deferred failures must be transient and explicit retry delays must be positive.");
			}

			logger.Log(classification.Kind == JobFailureKind.Permanent ? LogLevel.Error : LogLevel.Warning,
				"Job {JobId} attempt failed with {FailureCode} ({FailureType})", id, classification.Code, failure.GetType().Name);
			var delay = classification.RetryAfter ?? (classification.Kind == JobFailureKind.Transient
				? options.Value.DefaultRetryDelay : (TimeSpan?)null);
			await execution.FinishAsync(job =>
			{
				var now = clock.GetUtcNow();
				var retry = delay is { } duration && duration < job.Details.Definition.Deadline - now
					? now + duration : (DateTimeOffset?)null;
				if (classification.Deferred && retry is { } deferred && deferred < job.Details.Definition.Deadline)
				{
					job.DeferForCapacity(classification.Code, now, deferred);
				}
				else
				{
					job.Fail(classification.Code, classification.Deferred ? JobFailureKind.Permanent : classification.Kind,
						now, retry is { } next && next < job.Details.Definition.Deadline ? next : null);
				}
			}, stoppingToken);
		}
		finally
		{
			await StopHeartbeatAsync();
		}
	}

	private async Task RenewLoopAsync(JobExecution<TJob> execution, CancellationTokenSource cancellation, CancellationToken token)
	{
		try
		{
			while (!token.IsCancellationRequested)
			{
				await Task.Delay(options.Value.RenewalInterval, clock, token);
				await execution.RenewAsync(token);
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			logger.LogDebug("Job renewal loop stopped");
		}
		catch (Exception failure)
		{
			logger.LogError("Job {JobId} heartbeat failed ({FailureType}); cancelling its handler", execution.Details.Definition.Id, failure.GetType().Name);
			await cancellation.CancelAsync();
		}
	}
}
