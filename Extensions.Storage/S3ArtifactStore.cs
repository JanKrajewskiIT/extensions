using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Extensions.Domain.Primitives;
using Extensions.Domain.Exceptions;

namespace Extensions.Storage;

/// <summary>Content-addressed S3 storage with exact versions and streaming integrity checks.</summary>
public sealed class S3ArtifactStore(IAmazonS3 client, IOptions<ObjectStoreOptions> options, IArtifactRequestExecutor executor) : IVersionedArtifactStore, IArtifactVersionRemover
{
	private const int BufferSize = 81920;
	private readonly ObjectStoreOptions settings = options.Value;

	/// <summary>Removes an exact content-addressed version after host retirement admission.</summary>
	public async ValueTask RemoveVersionAsync(ArtifactReference artifact, CancellationToken cancellationToken = default)
	{
		ValidateReference(artifact);

		await RequestAsync(token => client.DeleteObjectAsync(new DeleteObjectRequest
		{
			BucketName = settings.Bucket,
			Key = artifact.ObjectKey,
			VersionId = artifact.ObjectVersion,
		}, token), cancellationToken);
	}

	/// <summary>Buffers and validates the declared bytes before publishing a new immutable version.</summary>
	public async ValueTask<ArtifactReference> WriteImmutableAsync(ArtifactWriteRequest request,
		Stream content, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(content);
		DomainGuard.Required(request.ObjectKey, nameof(request.ObjectKey));
		DomainGuard.Required(request.MediaType, nameof(request.MediaType));
		ArgumentNullException.ThrowIfNull(request.ExpectedHash);
		ArgumentNullException.ThrowIfNull(request.RetentionPolicy);
		ArgumentNullException.ThrowIfNull(request.Entitlement);
		if (request.ContentLength < 0 || request.ContentLength > settings.MaximumObjectBytes)
		{
			throw new ArgumentOutOfRangeException(nameof(request.ContentLength));
		}

		var temporaryPath = Path.Combine(Path.GetTempPath(), $"extensions-artifact-{Guid.NewGuid():N}");
		await using var buffered = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite,
			FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		var buffer = new byte[BufferSize];
		long length = 0;
		int read;
		while ((read = await content.ReadAsync(buffer, cancellationToken)) != 0)
		{
			length = checked(length + read);
			if (length > request.ContentLength)
			{
				throw new InvalidDataException("Artifact content exceeds the declared length.");
			}

			hash.AppendData(buffer.AsSpan(0, read));
			await buffered.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		var digest = new ContentHash(Convert.ToHexString(hash.GetHashAndReset()));
		if (length != request.ContentLength || digest != request.ExpectedHash)
		{
			throw new InvalidDataException("Artifact length or checksum differs from the declared content.");
		}

		var versioning = await RequestAsync(token => client.GetBucketVersioningAsync(new GetBucketVersioningRequest
		{
			BucketName = settings.Bucket,
		}, token), cancellationToken);
		if (versioning.VersioningConfig.Status != VersionStatus.Enabled)
		{
			throw new InvalidOperationException("The artifact bucket must have versioning enabled.");
		}

		buffered.Position = 0;
		var key = $"{digest.Sha256}/{request.ObjectKey.TrimStart('/')}";
		var put = new PutObjectRequest
		{
			BucketName = settings.Bucket,
			Key = key,
			InputStream = buffered,
			ContentType = request.MediaType,
			AutoCloseStream = false,
			ChecksumSHA256 = Convert.ToBase64String(Convert.FromHexString(digest.Sha256)),
		};
		put.Metadata["sha256"] = digest.Sha256;
		put.Metadata["retention-policy"] = request.RetentionPolicy.ContentHash.Sha256;
		put.Metadata["entitlement"] = request.Entitlement.ContentHash.Sha256;
		var response = await RequestAsync(token => client.PutObjectAsync(put, token), cancellationToken);
		if (string.IsNullOrWhiteSpace(response.VersionId) || response.VersionId == "null")
		{
			throw new InvalidOperationException("The artifact store did not return an immutable object version.");
		}

		return new ArtifactReference(settings.StoreName, key, response.VersionId,
			request.MediaType, length, digest);
	}

	/// <summary>Reads only the declared version; checksum verification completes at EOF.</summary>
	public async ValueTask<Stream> OpenReadAsync(ArtifactReference artifact, CancellationToken cancellationToken = default)
	{
		ValidateReference(artifact);

		if (artifact.Length > settings.MaximumObjectBytes)
		{
			throw new ArgumentOutOfRangeException(nameof(artifact), "The artifact exceeds the configured read limit.");
		}

		var response = await RequestAsync(token => client.GetObjectAsync(new GetObjectRequest
		{
			BucketName = settings.Bucket,
			Key = artifact.ObjectKey,
			VersionId = artifact.ObjectVersion,
			ChecksumMode = ChecksumMode.ENABLED,
		}, token), cancellationToken);
		if (response.ContentLength != artifact.Length || response.VersionId != artifact.ObjectVersion)
		{
			response.Dispose();
			throw new InvalidDataException("Stored artifact version or length differs from its manifest.");
		}

		return new VerifiedArtifactStream(response, artifact.ContentHash);
	}

	private void ValidateReference(ArtifactReference artifact)
	{
		ArgumentNullException.ThrowIfNull(artifact);
		if (artifact.Store != settings.StoreName ||
			string.IsNullOrWhiteSpace(artifact.ObjectVersion) || artifact.ObjectVersion == "null" ||
			!artifact.ObjectKey.StartsWith(artifact.ContentHash.Sha256 + "/", StringComparison.Ordinal))
		{
			throw new ArgumentException("An exact content-addressed version in this store is required.", nameof(artifact));
		}
	}

	private async Task<T> RequestAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token)
	{
		try
		{
			return await executor.ExecuteAsync(action, token);
		}
		catch (AmazonS3Exception failure) when (failure.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
			(int)failure.StatusCode >= 500 || failure.InnerException is HttpRequestException or IOException or TimeoutException)
		{
			throw new DependencyFailureException("artifact_store.unavailable", true, TimeSpan.FromSeconds(5), failure);
		}
	}

}
