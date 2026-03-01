using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Infrastructure.Caching;

public static class CacheExtensions
{
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
