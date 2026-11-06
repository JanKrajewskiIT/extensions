namespace Extensions.Infrastructure.Database;

/// <summary>Controls the default execution strategy registered for a PostgreSQL context.</summary>
public enum PostgresRetryPolicy
{
    /// <summary>Leaves the provider's non-retrying default in place.</summary>
    NoRetries,

    /// <summary>Retries transient failures with the shared retry count and delay.</summary>
    TransientFailures,
}
