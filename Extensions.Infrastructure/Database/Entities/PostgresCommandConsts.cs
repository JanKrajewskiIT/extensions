namespace Extensions.Infrastructure.Database.Entities;

/// <summary>
/// PostgreSQL-specific SQL fragments used in EF Core entity configurations.
/// </summary>
public static class PostgresCommandConsts
{
    /// <summary>SQL expression that returns the current transaction timestamp.</summary>
    public const string CurrentTimeStamp = "CURRENT_TIMESTAMP";

    /// <summary>PostgreSQL column type for binary JSON storage.</summary>
    public const string JsonDataType = "jsonb";
}
