using Extensions.Domain.Abstractions;
using Extensions.Infrastructure.Caching;
using Extensions.Infrastructure.Cors;
using Extensions.Infrastructure.Handlers;
using Extensions.Infrastructure.Metrics;
using Extensions.Infrastructure.OpenApi;
using Extensions.Infrastructure.RateLimiting;
using Extensions.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Extensions.Infrastructure;

/// <summary>
/// Top-level bootstrapping helpers for ASP.NET Core modules.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Builds and runs a module: creates the builder, wires common defaults,
    /// invokes <paramref name="configureServices"/> and <paramref name="configureApp"/>,
    /// optionally executes <paramref name="beforeRun"/> (e.g. database migrations),
    /// then starts the host.
    /// With --export-openapi &lt;output.json&gt;, briefly starts an ephemeral loopback host to export mapped endpoints without invoking beforeRun.
    /// </summary>
    public static async Task RunModuleAsync(
        string[] args,
        Action<WebApplicationBuilder> configureServices,
        Action<WebApplication> configureApp,
        Func<IServiceProvider, CancellationToken, Task>? beforeRun = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configureServices);
        ArgumentNullException.ThrowIfNull(configureApp);
        var exportPath = OpenApiContractExport.GetOutputPath(args);
        var builder = WebApplication.CreateBuilder(exportPath is null ? args : Array.Empty<string>());
        if (exportPath is not null)
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        }

        var keycloakSection = builder.Configuration.GetRequiredSection("Keycloak");
        builder.AddServiceDefaults(keycloakSection);

        var hostServicesBeforeModuleConfiguration = exportPath is null
            ? null
            : builder.Services
                .Where(service => service.ServiceType == typeof(IHostedService))
                .ToHashSet();

        configureServices(builder);
        if (hostServicesBeforeModuleConfiguration is not null)
        {
            foreach (var hostedService in builder.Services
                .Where(service => service.ServiceType == typeof(IHostedService)
                    && !hostServicesBeforeModuleConfiguration.Contains(service))
                .ToArray())
            {
                builder.Services.Remove(hostedService);
            }
        }

        await using var app = builder.Build();
        if (exportPath is null)
        {
            app.UseCommonConfiguration();
        }

        configureApp(app);

        if (exportPath is not null)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.StartAsync(deadline.Token);
            await OpenApiContractExport.WriteAsync(app, exportPath, deadline.Token);
            return;
        }

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
        builder.AddHostDefaults();

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

    /// <summary>
    /// Registers telemetry, process health checks, service discovery and HTTP resilience
    /// without requiring authentication, database or API configuration.
    /// </summary>
    /// <param name="builder">The consuming host builder.</param>
    /// <param name="configureResilience">Optional host-owned HTTP resilience policy.</param>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <returns>The consuming host builder.</returns>
    public static TBuilder AddHostDefaults<TBuilder>(
        this TBuilder builder,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddApplicationMetrics();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            var resilience = http.AddStandardResilienceHandler();
            if (configureResilience is not null)
            {
                resilience.Configure(configureResilience);
            }

            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static void ConfigureJsonOptions(this IServiceCollection services)
    {
        var enumConverter = new JsonStringEnumConverter();

        services.Configure<JsonOptions>(options => ApplyJsonSettings(options.SerializerOptions, enumConverter));
        services.ConfigureHttpJsonOptions(options => ApplyJsonSettings(options.SerializerOptions, enumConverter));
    }

    private static void ApplyJsonSettings(JsonSerializerOptions options, JsonStringEnumConverter enumConverter)
    {
        options.Converters.Add(enumConverter);
        options.AllowTrailingCommas = false;
        options.ReadCommentHandling = JsonCommentHandling.Disallow;
    }
}
