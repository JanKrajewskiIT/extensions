using Extensions.Domain.Abstractions;
using Extensions.Infrastructure.Caching;
using Extensions.Infrastructure.Cors;
using Extensions.Infrastructure.Handlers;
using Extensions.Infrastructure.Metrics;
using Extensions.Infrastructure.OpenApi;
using Extensions.Infrastructure.RateLimiting;
using Extensions.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Extensions.Infrastructure;

/// <summary>
/// Top-level bootstrapping helpers shared by all Recron microservices.
/// </summary>
public static class ConfigurationExtensions
{
	/// <summary>
	/// Builds and runs a Recron module: creates the builder, wires common defaults,
	/// invokes <paramref name="configureServices"/> and <paramref name="configureApp"/>,
	/// optionally executes <paramref name="beforeRun"/> (e.g. database migrations),
	/// then starts the host.
	/// </summary>
	public static async Task RunModuleAsync(
		string[] args,
		Action<WebApplicationBuilder> configureServices,
		Action<WebApplication> configureApp,
		Func<IServiceProvider, CancellationToken, Task>? beforeRun = null)
	{
		var builder = WebApplication.CreateBuilder(args);

		var keycloakSection = builder.Configuration.GetRequiredSection("Keycloak");
		builder.AddServiceDefaults(keycloakSection);

		configureServices(builder);

		var app = builder.Build();
		app.UseCommonConfiguration();

		configureApp(app);

		if (beforeRun is not null)
		{
			await beforeRun(app.Services, app.Lifetime.ApplicationStopping);
		}

		await app.RunAsync();
	}

	/// <summary>
	/// Registers all shared infrastructure services: logging, metrics, service discovery,
	/// HTTP client resilience, OpenAI (when configured), auth, CORS, rate limiting,
	/// OpenAPI, problem details, hybrid caching, and JSON options.
	/// </summary>
	public static WebApplicationBuilder AddServiceDefaults(
		this WebApplicationBuilder builder,
		IConfigurationSection keycloakSection)
	{
		builder.AddStructuredLogging();
		builder.AddApplicationMetrics();

		builder.Services.AddServiceDiscovery();
		builder.Services.ConfigureHttpClientDefaults(http =>
		{
			http.AddStandardResilienceHandler();
			http.AddServiceDiscovery();
		});

		var chatConnectionString = builder.Configuration.GetConnectionString("chat");
		if (!string.IsNullOrWhiteSpace(chatConnectionString))
		{
			builder.AddOpenAIClient("chat").AddChatClient();
		}

		builder.Services.AddHttpContextAccessor();
		builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

		builder.Services.AddAllCors(builder.Configuration, builder.Environment);
		builder.Services.AddAppRateLimiting(builder.Configuration);
		builder.Services.AddAuth(keycloakSection);

		builder.Services.AddOpenApiWithAuth(keycloakSection);
		builder.Services.AddEndpointsApiExplorer();

		builder.Services.AddProblemDetails();
		builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
		builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

		builder.Services.AddHybridCaching();

		builder.Services.ConfigureJsonOptions();

		return builder;
	}

	/// <summary>
	/// Adds shared middleware: structured logging, health checks, CORS, rate limiting,
	/// authentication/authorization, OpenAPI, and exception handling.
	/// </summary>
	public static IApplicationBuilder UseCommonConfiguration(this WebApplication app)
	{
		app.UseStructuredLogging();
		app.UseDefaultHealthChecks();

		app.UseAllCors();
		app.UseAppRateLimiter();
		app.UseAuth();

		app.UseOpenApiWithAuth();

		app.UseExceptionHandler();

		return app;
	}

	private static void ConfigureJsonOptions(this IServiceCollection services)
	{
		var enumConverter = new JsonStringEnumConverter();

		services.Configure<JsonOptions>(options => ApplyJsonSettings(options.SerializerOptions, enumConverter));
		services.ConfigureHttpJsonOptions(options => ApplyJsonSettings(options.SerializerOptions, enumConverter));
	}

	private static void ApplyJsonSettings(System.Text.Json.JsonSerializerOptions options, JsonStringEnumConverter enumConverter)
	{
		options.Converters.Add(enumConverter);
		options.AllowTrailingCommas = false;
		options.ReadCommentHandling = JsonCommentHandling.Disallow;
	}
}
