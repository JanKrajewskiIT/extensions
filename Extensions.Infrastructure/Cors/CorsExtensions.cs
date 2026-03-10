using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Cors;

internal static class CorsExtensions
{
	private const string AllCors = "allCors";

	private static readonly string[] ExposedHeaders =
		["X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset"];

	internal static IServiceCollection AddAllCors(
		this IServiceCollection services,
		IConfiguration configuration,
		IHostEnvironment environment)
	{
		var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

		if (allowedOrigins is not { Length: > 0 })
		{
			if (!environment.IsDevelopment())
			{
				throw new InvalidOperationException(
					"Missing required configuration: Cors:AllowedOrigins. " +
					"Set explicit origins for non-development environments.");
			}

			using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
			var logger = loggerFactory.CreateLogger("Extensions.Infrastructure.Cors");
			logger.LogWarning(
				"No 'Cors:AllowedOrigins' configured in Development - using AllowAnyOrigin(). " +
				"Set Cors:AllowedOrigins to mirror production behavior.");
		}

		services.AddCors(options =>
		{
			options.AddPolicy(AllCors, policy =>
			{
				policy.AllowAnyHeader()
					.AllowAnyMethod()
					.WithExposedHeaders(ExposedHeaders);

				if (allowedOrigins is { Length: > 0 })
				{
					policy.WithOrigins(allowedOrigins).AllowCredentials();
				}
				else
				{
					policy.AllowAnyOrigin();
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