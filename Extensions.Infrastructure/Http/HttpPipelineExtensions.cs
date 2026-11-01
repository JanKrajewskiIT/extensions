using Extensions.Infrastructure.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Extensions.Infrastructure.Http;

/// <summary>Explicit registration and middleware composition; RunModuleAsync remains available unchanged.</summary>
public static class HttpPipelineExtensions
{
	/// <summary>Registers safe common exception responses without enabling request deadlines or audit filters.</summary>
	public static IServiceCollection AddHttpProblemResponses(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<TimeProvider>(TimeProvider.System);
		services.TryAddSingleton<IHttpExceptionMapper, DefaultHttpExceptionMapper>();
		services.AddProblemDetails();
		services.AddExceptionHandler<ValidationExceptionHandler>();
		services.AddExceptionHandler<GlobalExceptionHandler>();
		return services;
	}

	/// <summary>Opts into request budgeting and an explicit application error mapping.</summary>
	public static IServiceCollection AddHttpPipeline<TMapper>(this IServiceCollection services,
		Action<HttpPipelineOptions>? configure = null) where TMapper : class, IHttpExceptionMapper
	{
		services.AddHttpProblemResponses();
		services.Replace(ServiceDescriptor.Singleton<IHttpExceptionMapper, TMapper>());
		var options = services.AddOptions<HttpPipelineOptions>();
		if (configure is not null)
		{
			options.Configure(configure);
		}

		options.Validate(value => value.DefaultRequestDuration > TimeSpan.Zero && value.DefaultRequestDuration <= RequestBudgetLimit.MaximumDuration,
			"The default HTTP deadline must be positive and no longer than one day.")
			.Validate(value => !string.IsNullOrWhiteSpace(value.DeadlineCode) &&
				value.DeadlineCode == HttpProblemResponses.SafeCode(value.DeadlineCode, string.Empty),
				"The HTTP deadline requires a bounded machine code.")
			.ValidateOnStart();
		services.TryAddSingleton<IRequestBudgetScopeFactory>(new EmptyRequestBudgetScope());
		return services;
	}

	/// <summary>Use after routing and inside exception handling so endpoint metadata and deadline mapping are honored.</summary>
	public static IApplicationBuilder UseHttpRequestPipeline(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);
		return app.UseMiddleware<HttpRequestPipelineMiddleware>();
	}

	/// <summary>Applies a host-supplied CSP and security headers only to a chosen static console path.</summary>
	public static IApplicationBuilder UseStaticConsoleSecurityHeaders(this IApplicationBuilder app, PathString path, string contentSecurityPolicy)
	{
		ArgumentNullException.ThrowIfNull(app);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentSecurityPolicy);
		if (!path.HasValue || contentSecurityPolicy.Any(value => value is '\r' or '\n'))
		{
			throw new ArgumentException("A console path and a single-line CSP are required.", nameof(path));
		}

		return app.Use(async (context, next) =>
		{
			if (context.Request.Path.StartsWithSegments(path))
			{
				context.Response.OnStarting(() =>
				{
					context.Response.Headers["Content-Security-Policy"] = contentSecurityPolicy;
					context.Response.Headers["Referrer-Policy"] = "no-referrer";
					context.Response.Headers["X-Content-Type-Options"] = "nosniff";
					context.Response.Headers.CacheControl = "no-store";
					return Task.CompletedTask;
				});
			}

			await next(context);
		});
	}

	private sealed class EmptyRequestBudgetScope : IRequestBudgetScopeFactory, IDisposable
	{
		public IDisposable Begin(HttpContext context, CancellationToken cancellationToken) => this;

		public void Dispose()
		{
		}
	}
}
