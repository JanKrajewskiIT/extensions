using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Extensions.Storage;

/// <summary>Opt-in storage registration; it does not provision buckets, grant access or install a lineage hook.</summary>
public static class StorageRegistrationExtensions
{
	/// <summary>Registers S3 with startup validation and explicit credential-factory opt-in.</summary>
	public static IServiceCollection AddS3ArtifactStorage(this IServiceCollection services, Action<ObjectStoreOptions> configure,
		Func<IServiceProvider, AWSCredentials>? staticCredentials = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		services.AddOptions<ObjectStoreOptions>().Configure(configure)
			.Validate(ObjectStoreOptions.Valid, "Object store requires bounded identity, region, size, timeout and an HTTPS origin.")
			.Validate(value => value.UseStaticCredentials == (staticCredentials is not null),
				"Static credentials require matching explicit opt-in and a host credential factory.")
			.ValidateOnStart();
		services.TryAddSingleton<IAmazonS3>(provider =>
		{
			var value = provider.GetRequiredService<IOptions<ObjectStoreOptions>>().Value;
			var settings = new AmazonS3Config
			{
				RegionEndpoint = RegionEndpoint.GetBySystemName(value.Region),
				ForcePathStyle = value.ForcePathStyle,
				MaxErrorRetry = 0,
				Timeout = value.RequestTimeout,
			};
			if (value.ServiceUrl is { } url)
			{
				settings.ServiceURL = url;
				settings.AuthenticationRegion = value.Region;
			}

			return value.UseStaticCredentials
				? new AmazonS3Client(staticCredentials!(provider), settings)
				: new AmazonS3Client(settings);
		});
		services.TryAddSingleton<IArtifactRequestExecutor, DirectArtifactRequestExecutor>();
		services.TryAddScoped<S3ArtifactStore>();
		services.TryAddScoped<IVersionedArtifactStore>(provider => provider.GetRequiredService<S3ArtifactStore>());
		services.TryAddScoped<IArtifactVersionRemover>(provider => provider.GetRequiredService<S3ArtifactStore>());
		services.TryAddSingleton<ObjectStoreDependencyHealthCheck>();
		return services;
	}
}
