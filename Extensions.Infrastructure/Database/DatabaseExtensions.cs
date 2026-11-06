using Extensions.Infrastructure.Database.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Enables dynamic JSON, up to 3 retries on failure, and wires the <see cref="AuditInterceptor"/>.
    /// Parameter logging is enabled automatically in Development.
    /// </summary>
    public static IHostApplicationBuilder AddPostgresDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null)
        where TContext : DbContext
    {
        return builder.AddPostgresDbContext<TContext>(connectionName, PostgresRetryPolicy.TransientFailures,
            configureDataSource, configureNpgsql);
    }

    /// <summary>
    /// Registers a PostgreSQL context with an explicit default retry policy.
    /// The Npgsql callback runs afterwards and owns any strategy override.
    /// </summary>
    public static IHostApplicationBuilder AddPostgresDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string connectionName,
        PostgresRetryPolicy retryPolicy,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(retryPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(retryPolicy),
                $"The PostgreSQL retry policy must be one of: {string.Join(", ", Enum.GetNames<PostgresRetryPolicy>())}.");
        }

        builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.TryAddScoped<IAuditActorProvider, CurrentUserAuditActorProvider>();
        builder.Services.AddScoped(provider => new AuditInterceptor(
            provider.GetRequiredService<IAuditActorProvider>(), provider.GetRequiredService<TimeProvider>()));

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
                if (retryPolicy == PostgresRetryPolicy.TransientFailures)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }

                configureNpgsql?.Invoke(npgsqlOptions);
            });

            var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
            options.AddInterceptors(auditInterceptor);
        });

        return builder;
    }
}
