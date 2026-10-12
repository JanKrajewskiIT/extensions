using Extensions.Infrastructure.Database.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Registration helpers for PostgreSQL-backed <see cref="DbContext"/> instances.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Registers a PostgreSQL data source and <typeparamref name="TContext"/> with the DI container.
    /// Enables dynamic JSON, retry-on-failure (3 attempts), and wires the <see cref="AuditInterceptor"/>.
    /// Parameter logging is enabled automatically in Development.
    /// </summary>
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
