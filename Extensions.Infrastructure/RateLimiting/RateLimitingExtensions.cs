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
        var globalOptions = section.Get<RateLimitOptions>() ?? new RateLimitOptions();
        var aiOptions = section.GetSection("Ai").Get<RateLimitOptions>() ?? new RateLimitOptions(PermitLimit: 10);

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = 429;

            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                CreateFixedWindowPartition(context, globalOptions));

            limiterOptions.AddPolicy(RateLimitPolicies.Ai, context =>
                CreateFixedWindowPartition(context, aiOptions));

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

    private static RateLimitPartition<string> CreateFixedWindowPartition(HttpContext context, RateLimitOptions options) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
}

internal sealed record RateLimitOptions(int PermitLimit = 100, int WindowSeconds = 60);
