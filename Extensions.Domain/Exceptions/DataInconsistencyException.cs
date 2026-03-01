namespace Extensions.Domain.Exceptions;

/// <summary>
/// Exception thrown when there is a data inconsistency in the domain model.
/// This indicates a violation of business rules or domain constraints.
/// </summary>
public sealed class DataInconsistencyException : Exception
{
	public DataInconsistencyException(string message) : base(message)
	{
	}

	public DataInconsistencyException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
