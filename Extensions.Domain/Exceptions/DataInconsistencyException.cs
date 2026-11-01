namespace Extensions.Domain.Exceptions;

/// <summary>
/// Exception thrown when there is a data inconsistency in the domain model.
/// This indicates a violation of business rules or domain constraints.
/// </summary>
public sealed class DataInconsistencyException : Exception, ICodedException
{
    /// <summary>Default machine code retained by the original message-based constructors.</summary>
    public const string DefaultCode = "data.inconsistent";

    /// <inheritdoc />
    public string Code { get; } = DefaultCode;

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.DataInconsistency;

    /// <summary>Initializes a new instance with the specified error <paramref name="message"/>.</summary>
    public DataInconsistencyException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance with the specified error <paramref name="message"/> and <paramref name="innerException"/>.</summary>
    public DataInconsistencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates a coded inconsistency while preserving its diagnostic message and cause.</summary>
    public DataInconsistencyException(string code, string message, Exception? innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }
}
