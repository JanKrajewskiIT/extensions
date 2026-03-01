using Extensions.Infrastructure.Database.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Extensions.Infrastructure.Database;

public static class DatabaseExtensions
{
	public static IHostApplicationBuilder AddPostgresDbContext<TContext>(
		this IHostApplicationBuilder builder,
		string connectionName,
		Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
		Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null)
		where TContext : DbContext
	{
		builder.Services.AddScoped<AuditInterceptor>();

		builder.AddNpgsqlDataSource(
			connectionName,
			configureDataSourceBuilder: options =>
			{
				options.EnableDynamicJson();
				options.EnableUnmappedTypes();
				options.EnableRecordsAsTuples();

				if (builder.Environment.IsDevelopment())
				{
					options.EnableParameterLogging();
				}

				configureDataSource?.Invoke(options);
			});

		builder.Services.AddDbContext<TContext>((serviceProvider, options) =>
		{
			var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();

			options.UseNpgsql(dataSource, npgsqlOptions =>
			{
				npgsqlOptions.EnableRetryOnFailure(
					maxRetryCount: 3,
					maxRetryDelay: TimeSpan.FromSeconds(5),
					errorCodesToAdd: null);

				configureNpgsql?.Invoke(npgsqlOptions);
			});

			var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
			options.AddInterceptors(auditInterceptor);
		});

		return builder;
	}
}
