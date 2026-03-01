using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;

namespace Extensions.Infrastructure.Metrics;

public static class LoggingExtensions
{
	public static WebApplicationBuilder AddStructuredLogging(this WebApplicationBuilder builder)
	{
		Log.Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(builder.Configuration)
			.Enrich.FromLogContext()
			.Enrich.WithMachineName()
			.Enrich.WithThreadId()
			.Enrich.WithEnvironmentName()
			.Enrich.WithProperty("ApplicationName", builder.Environment.ApplicationName)
			.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
			.WriteTo.OpenTelemetry(options =>
			{
				options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
				options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
				options.ResourceAttributes = new Dictionary<string, object>
				{
					["service.name"] = builder.Environment.ApplicationName
				};
			})
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
			.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
			.MinimumLevel.Override("System", LogEventLevel.Warning)
			.CreateLogger();

		builder.Host.UseSerilog();

		return builder;
	}

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
