using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace Extensions.Storage;

/// <summary>Explicit administrative bucket provisioning; never invoked by storage registration.</summary>
public static class ObjectStoreProvisioning
{
	/// <summary>Creates a missing bucket and confirms enabled versioning using a separately authorized admin client.</summary>
	public static async Task ProvisionAsync(IAmazonS3 client, string bucket, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
		try
		{
			await client.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = bucket }, cancellationToken);
		}
		catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound && exception.ErrorCode == "NoSuchBucket")
		{
			await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket, UseClientRegion = true }, cancellationToken);
		}

		await client.PutBucketVersioningAsync(new PutBucketVersioningRequest
		{
			BucketName = bucket,
			VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled },
		}, cancellationToken);
		var versioning = await client.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = bucket }, cancellationToken);
		if (versioning.VersioningConfig.Status != VersionStatus.Enabled)
		{
			throw new InvalidOperationException("Object-store provisioning did not confirm enabled versioning.");
		}
	}
}
