using Extensions.Domain.Exceptions;

namespace Extensions.Infrastructure.Database;

/// <summary>A bounded connection-probe sequence exhausted its attempts before migration began.</summary>
public sealed class DatabaseStartupException : Exception, ICodedException
{
    /// <summary>Stable code for an unavailable startup database.</summary>
    public const string FailureCode = "database.startup_unavailable";

    /// <summary>Creates a safe summary and retains an original cause when the caller has one.</summary>
    public DatabaseStartupException(int attempts, Exception? innerException = null)
        : base($"{FailureCode}: database connection was unavailable after {attempts} attempts.", innerException)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        Attempts = attempts;
    }

    /// <summary>Number of completed connection probes.</summary>
    public int Attempts { get; }

    /// <inheritdoc />
    public string Code => FailureCode;

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.Dependency;
}
