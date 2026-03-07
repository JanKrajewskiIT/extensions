using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace Extensions.Infrastructure.RateLimiting;

/// <summary>Named rate limiting policy identifiers for use with <c>.RequireRateLimiting()</c>.</summary>
public static class RateLimitPolicies
{
	/// <summary>Stricter rate limit for AI/LLM endpoints (default: 10 requests per 60 seconds).</summary>
	public const string Ai = "ai";
}

internal static class RateLimitingExtensions
{
	private const string SectionName = "RateLimiting";

	internal static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection(SectionName);
		var globalOptions = section.Get<AppRateLimitOptions>() ?? new AppRateLimitOptions();
		var aiOptions = section.GetSection("Ai").Get<AiRateLimitOptions>() ?? new AiRateLimitOptions();

		services.AddRateLimiter(limiterOptions =>
		{
			limiterOptions.RejectionStatusCode = 429;

			limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = globalOptions.PermitLimit,
						Window = TimeSpan.FromSeconds(globalOptions.WindowSeconds),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					}));

			limiterOptions.AddPolicy(RateLimitPolicies.Ai, context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = aiOptions.PermitLimit,
						Window = TimeSpan.FromSeconds(aiOptions.WindowSeconds),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					}));

			limiterOptions.OnRejected = async (context, cancellationToken) =>
			{
				context.HttpContext.Response.StatusCode = 429;

				if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
				{
					context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
				}

				await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
			};
		});

		return services;
	}

	internal static IApplicationBuilder UseAppRateLimiter(this IApplicationBuilder app)
	{
		app.UseRateLimiter();
		return app;
	}
}

internal sealed class AppRateLimitOptions
{
	/// <summary>Number of requests allowed per window. Defaults to 100.</summary>
	public int PermitLimit { get; init; } = 100;

	/// <summary>Length of the fixed window in seconds. Defaults to 60.</summary>
	public int WindowSeconds { get; init; } = 60;
}

internal sealed class AiRateLimitOptions
{
	/// <summary>Number of AI requests allowed per window. Defaults to 10.</summary>
	public int PermitLimit { get; init; } = 10;

	/// <summary>Length of the fixed window in seconds. Defaults to 60.</summary>
	public int WindowSeconds { get; init; } = 60;
}
