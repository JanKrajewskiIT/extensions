namespace Extensions.Domain.Exceptions;

/// <summary>
/// Exception thrown when there is a data inconsistency in the domain model.
/// This indicates a violation of business rules or domain constraints.
/// </summary>
public sealed class DataInconsistencyException : Exception
{
    /// <summary>Initializes a new instance with the specified error <paramref name="message"/>.</summary>
    public DataInconsistencyException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance with the specified error <paramref name="message"/> and <paramref name="innerException"/>.</summary>
    public DataInconsistencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
