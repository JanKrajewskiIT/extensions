namespace Extensions.Application.Pagination;

/// <summary>
/// Base record for paginated queries. Provides Page and PageSize with sensible defaults.
/// When PageSize is 0 (default), all records are returned without pagination.
/// </summary>
public abstract record PagedQuery<TResponse>(
	int Page = 1,
	int PageSize = 0
) : Abstractions.IQuery<PagedResponse<TResponse>>;
