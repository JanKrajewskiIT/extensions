using System.Diagnostics;
using System.Globalization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public sealed class KafkaTopologyService(KafkaTopicCatalog catalog, IOptions<KafkaOptions> kafkaOptions,
	IOptions<KafkaTopologyOptions> topologyOptions, ILogger<KafkaTopologyService> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var options = topologyOptions.Value;
		if (!options.Enabled)
		{
			return;
		}

		var connection = kafkaOptions.Value;
		var adminConfiguration = new AdminClientConfig
		{
			BootstrapServers = connection.BootstrapServers,
			SecurityProtocol = connection.SecurityProtocol,
			SaslMechanism = connection.SaslMechanism,
			SaslUsername = connection.Username,
			SaslPassword = connection.Password,
			SslCaLocation = connection.SslCaLocation,
		};
		adminConfiguration.Set("allow.auto.create.topics", "false");
		using var admin = new AdminClientBuilder(adminConfiguration).Build();
		var configuration = new Dictionary<string, string>
		{
			["cleanup.policy"] = "delete",
			["unclean.leader.election.enable"] = "false",
			["min.insync.replicas"] = options.MinimumInSyncReplicas.ToString(CultureInfo.InvariantCulture),
			["retention.ms"] = checked((long)options.Retention.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
			["retention.bytes"] = options.RetentionBytesPerPartition.ToString(CultureInfo.InvariantCulture),
		};
		var topics = catalog.Snapshot();
		// Listing the cluster avoids requesting an unknown name, which can trigger broker auto-creation.
		var existing = admin.GetMetadata(options.RequestTimeout).Topics.ToDictionary(value => value.Topic, StringComparer.Ordinal);
		foreach (var topic in topics)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!existing.TryGetValue(topic, out var metadata) || metadata.Error.Code == ErrorCode.UnknownTopicOrPart)
			{
				if (!options.CreateMissing)
				{
					throw new InvalidOperationException($"Kafka topic '{topic}' is missing and Kafka:Topology:CreateMissing is disabled.");
				}

				try
				{
					await admin.CreateTopicsAsync([new TopicSpecification
					{
						Name = topic, NumPartitions = options.Partitions, ReplicationFactor = options.ReplicationFactor,
						Configs = configuration,
					},], new CreateTopicsOptions { RequestTimeout = options.RequestTimeout, OperationTimeout = options.RequestTimeout })
						.WaitAsync(cancellationToken);
				}
				catch (CreateTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
				{
					logger.LogInformation("Kafka topic {Topic} was concurrently provisioned", topic);
				}
			}

			metadata = await WaitForTopicAsync(admin, topic, options.RequestTimeout, cancellationToken);
			if (metadata.Partitions.Count != options.Partitions ||
				metadata.Partitions.Any(partition => partition.Replicas.Length != options.ReplicationFactor))
			{
				var replicas = string.Join(", ", metadata.Partitions.Select(partition => $"{partition.PartitionId}:{partition.Replicas.Length}"));
				throw new InvalidOperationException($"Kafka topic '{topic}' differs from Kafka:Topology: " +
					$"expected {options.Partitions} partitions and replication factor {options.ReplicationFactor}; " +
					$"actual {metadata.Partitions.Count} partitions, replicas by partition [{replicas}]. " +
					"Align the configured contract or explicitly migrate the existing topic; startup does not repartition stored messages.");
			}

			var descriptions = await admin.DescribeConfigsAsync([new ConfigResource { Type = ResourceType.Topic, Name = topic }],
				new DescribeConfigsOptions { RequestTimeout = options.RequestTimeout }).WaitAsync(cancellationToken);
			var actual = descriptions.Single().Entries;
			var differences = configuration.Select(setting => new
			{
				setting.Key,
				Expected = setting.Value,
				Actual = actual.TryGetValue(setting.Key, out var entry) ? entry.Value : null,
			}).Where(setting => setting.Expected != setting.Actual).ToArray();
			if (differences.Length > 0)
			{
				var details = string.Join("; ", differences.Select(setting =>
					$"{setting.Key}: expected '{setting.Expected}', actual '{setting.Actual ?? "<missing>"}'"));
				throw new InvalidOperationException($"Kafka topic '{topic}' durability or retention differs from Kafka:Topology. {details}.");
			}
		}

		logger.LogInformation("Kafka topology is ready for {TopicCount} registered topics", topics.Length);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private static async Task<TopicMetadata> WaitForTopicAsync(IAdminClient admin, string topic,
		TimeSpan timeout, CancellationToken cancellationToken)
	{
		var elapsed = Stopwatch.StartNew();
		TopicMetadata? metadata = null;
		while (elapsed.Elapsed < timeout)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var remaining = timeout - elapsed.Elapsed;
			if (remaining <= TimeSpan.Zero)
			{
				break;
			}

			metadata = admin.GetMetadata(remaining).Topics.SingleOrDefault(value => value.Topic == topic);
			if (metadata is not null)
			{
				if (metadata.Error.IsError && metadata.Error.Code is not (ErrorCode.UnknownTopicOrPart or ErrorCode.LeaderNotAvailable))
				{
					throw new InvalidOperationException($"Kafka topic '{topic}' metadata lookup failed with {metadata.Error.Code}.");
				}

				if (!metadata.Error.IsError && metadata.Partitions.Count > 0 &&
					metadata.Partitions.All(partition => !partition.Error.IsError && partition.Leader >= 0 && partition.Replicas.Length > 0))
				{
					return metadata;
				}
			}

			remaining = timeout - elapsed.Elapsed;
			if (remaining > TimeSpan.Zero)
			{
				await Task.Delay(remaining < TimeSpan.FromMilliseconds(250) ? remaining : TimeSpan.FromMilliseconds(250), cancellationToken);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		throw new TimeoutException($"Kafka topic '{topic}' metadata did not become ready within {timeout}. " +
			$"Last topic error: {metadata?.Error.Code.ToString() ?? "topic not visible"}.");
	}
}
