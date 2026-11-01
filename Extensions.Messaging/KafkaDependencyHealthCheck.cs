using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public sealed class KafkaDependencyHealthCheck(IOptions<KafkaOptions> options) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		var settings = options.Value;
		try
		{
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
			return metadata.Brokers.Count > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("No Kafka brokers available.");
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return HealthCheckResult.Unhealthy("Kafka authentication or metadata request failed.");
		}
	}
}
