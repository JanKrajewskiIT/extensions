using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Extensions.Messaging;

internal static class KafkaRetryBackoff
{
	public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	public static TimeSpan Delay(KafkaSubscriptionOptions settings, int failedAttempt)
	{
		var upper = Math.Min(settings.RetryMaximumDelay.TotalMilliseconds,
			settings.RetryInitialDelay.TotalMilliseconds * Math.Pow(2, failedAttempt - 1));
		return TimeSpan.FromMilliseconds(upper * (0.5 + Random.Shared.NextDouble() * 0.5));
	}

	public static bool Wait(IConsumer<string, string> consumer, IReadOnlyDictionary<TopicPartition, long> blocked,
		Func<bool> ownsRecord, TimeSpan delay,
		TimeProvider clock, ILogger logger, CancellationToken token)
	{
		var started = clock.GetTimestamp();
		Exception? primaryFailure = null;
		try
		{
			while (clock.GetElapsedTime(started) < delay)
			{
				token.ThrowIfCancellationRequested();
				if (!ownsRecord())
				{
					return false;
				}

				var assigned = consumer.Assignment;
				if (assigned.Count > 0)
				{
					consumer.Pause(assigned);
				}

				var remaining = delay - clock.GetElapsedTime(started);
				var pollFor = remaining > PollInterval ? PollInterval :
					TimeSpan.FromMilliseconds(Math.Max(1, Math.Ceiling(remaining.TotalMilliseconds)));
				var polled = consumer.Consume(pollFor);
				if (polled is not null && !polled.IsPartitionEOF)
				{
					RewindIfAssigned(consumer, polled);
				}
			}

			token.ThrowIfCancellationRequested();
			return ownsRecord();
		}
		catch (Exception exception)
		{
			primaryFailure = exception;
			throw;
		}
		finally
		{
			try
			{
				ResumeUnblocked(consumer, blocked);
			}
			catch (KafkaException exception) when (primaryFailure is not null)
			{
				logger.LogWarning("Kafka retry pause cleanup failed with {ErrorCode}; the original failure is retained", exception.Error.Code);
			}
		}
	}

	public static void ResumeUnblocked(IConsumer<string, string> consumer, IReadOnlyDictionary<TopicPartition, long> blocked)
	{
		var resumable = consumer.Assignment.Where(partition => !blocked.ContainsKey(partition)).ToArray();
		if (resumable.Length > 0)
		{
			consumer.Resume(resumable);
		}
	}

	private static void RewindIfAssigned(IConsumer<string, string> consumer, ConsumeResult<string, string> record)
	{
		if (consumer.Assignment.Contains(record.TopicPartition))
		{
			consumer.Seek(record.TopicPartitionOffset);
		}
	}
}
