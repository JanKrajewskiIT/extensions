namespace Extensions.Application.Abstractions;

/// <summary>
/// Handler interface for queries that return a value
/// </summary>
/// <typeparam name="TQuery">The type of query</typeparam>
/// <typeparam name="TResponse">The type of response</typeparam>
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
	/// <summary>Handles the specified <paramref name="query"/> and returns a result.</summary>
	Task<TResponse> ExecuteAsync(TQuery query, CancellationToken cancellationToken = default);
}
