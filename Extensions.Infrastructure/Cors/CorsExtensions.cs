using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Cors;

internal static class CorsExtensions
{
	private const string AllCors = "allCors";

	internal static IServiceCollection AddAllCors(this IServiceCollection services, IConfiguration configuration)
	{
		var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

		if (allowedOrigins is not { Length: > 0 })
		{
			var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
			var logger = loggerFactory.CreateLogger("Extensions.Infrastructure.Cors");
			logger.LogWarning(
				"No 'Cors:AllowedOrigins' configured — falling back to AllowAnyOrigin(). " +
				"This is unsafe in production. Set Cors:AllowedOrigins in your configuration.");
		}

		services.AddCors(options =>
		{
			options.AddPolicy(AllCors, policy =>
			{
				if (allowedOrigins is { Length: > 0 })
				{
					policy.WithOrigins(allowedOrigins)
						.AllowAnyHeader()
						.AllowAnyMethod()
						.AllowCredentials()
						.WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
				}
				else
				{
					policy.AllowAnyOrigin()
						.AllowAnyHeader()
						.AllowAnyMethod()
						.WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
				}
			});
		});

		return services;
	}

	internal static IApplicationBuilder UseAllCors(this IApplicationBuilder app)
	{
		app.UseCors(AllCors);
		return app;
	}
}
