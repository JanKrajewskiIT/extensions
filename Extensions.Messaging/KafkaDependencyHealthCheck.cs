using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public sealed class KafkaDependencyHealthCheck(IOptions<KafkaOptions> options,
	Func<Exception?, HealthCheckResult>? failureResult) : IHealthCheck
{
	public KafkaDependencyHealthCheck(IOptions<KafkaOptions> options) : this(options, null)
	{
	}

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var settings = options.Value;
			using var admin = new AdminClientBuilder(new AdminClientConfig
			{
				BootstrapServers = settings.BootstrapServers,
				SecurityProtocol = settings.SecurityProtocol,
				SaslMechanism = settings.SaslMechanism,
				SaslUsername = settings.Username,
				SaslPassword = settings.Password,
				SslCaLocation = settings.SslCaLocation,
			}).Build();
			var metadata = await Task.Run(() => admin.GetMetadata(TimeSpan.FromSeconds(5)), cancellationToken);
			if (metadata.Brokers.Count > 0)
			{
				return HealthCheckResult.Healthy();
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException ||
			failureResult is not null && !cancellationToken.IsCancellationRequested)
		{
			return failureResult?.Invoke(exception)
				?? HealthCheckResult.Unhealthy("Kafka authentication or metadata request failed.");
		}

		return failureResult?.Invoke(null) ?? HealthCheckResult.Unhealthy("No Kafka brokers available.");
	}
}
