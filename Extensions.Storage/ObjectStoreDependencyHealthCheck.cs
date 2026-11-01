using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Extensions.Storage;

/// <summary>Non-disclosing readiness check for bucket access and enabled versioning.</summary>
public sealed class ObjectStoreDependencyHealthCheck(IAmazonS3 client, IOptions<ObjectStoreOptions> options) : IHealthCheck
{
	/// <summary>Checks access without provisioning resources or exposing dependency exception messages.</summary>
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await client.GetBucketVersioningAsync(new GetBucketVersioningRequest
			{
				BucketName = options.Value.Bucket,
			}, cancellationToken);
			return response.VersioningConfig.Status == VersionStatus.Enabled
				? HealthCheckResult.Healthy()
				: HealthCheckResult.Unhealthy("Artifact bucket versioning must be enabled.");
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return HealthCheckResult.Unhealthy("Artifact bucket access failed.");
		}
	}
}
