namespace Extensions.Application.Abstractions;

/// <summary>
/// Handler interface for queries that return a value
/// </summary>
/// <typeparam name="TQuery">The type of query</typeparam>
/// <typeparam name="TResponse">The type of response</typeparam>
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
	Task<TResponse> ExecuteAsync(TQuery query, CancellationToken cancellationToken = default);
}
