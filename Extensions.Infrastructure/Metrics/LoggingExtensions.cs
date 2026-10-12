using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;

namespace Extensions.Infrastructure.Metrics;

/// <summary>
/// Registration and middleware helpers for structured logging via Serilog.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures Serilog as the host logger: enriches with context, machine name, thread ID,
    /// environment name, and application name; writes to console and OpenTelemetry (OTLP).
    /// Log levels for <c>Microsoft</c>, <c>Microsoft.EntityFrameworkCore</c>, and <c>System</c>
    /// are overridden to <see cref="LogEventLevel.Warning"/> to reduce noise.
    /// </summary>
    public static WebApplicationBuilder AddStructuredLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, _, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
                    options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = context.HostingEnvironment.ApplicationName
                    };
                })
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning);
        });

        return builder;
    }

    /// <summary>
    /// Adds Serilog request logging middleware that emits one log entry per HTTP request,
    /// enriched with host, scheme, user agent, client IP, and (when authenticated) user name.
    /// </summary>
    public static WebApplication UseStructuredLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserName", httpContext.User.Identity.Name);
                }
            };
        });

        return app;
    }
}
