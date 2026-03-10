using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Infrastructure.Caching;

/// <summary>
/// Registration helpers for hybrid (in-memory + distributed) caching.
/// </summary>
public static class CacheExtensions
{
	/// <summary>
	/// Registers <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> with a 1 MB payload limit,
	/// 5-minute distributed expiration, and 1-minute local-cache expiration.
	/// </summary>
	public static IServiceCollection AddHybridCaching(this IServiceCollection services)
	{
		services.AddHybridCache(options =>
		{
			options.MaximumPayloadBytes = 1024 * 1024; // 1MB
			options.MaximumKeyLength = 1024;
			options.DefaultEntryOptions = new HybridCacheEntryOptions
			{
				Expiration = TimeSpan.FromMinutes(5),
				LocalCacheExpiration = TimeSpan.FromMinutes(1)
			};
		});

		return services;
	}
}
