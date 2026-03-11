namespace Extensions.Application.Pagination;

/// <summary>
/// Base record for paginated queries. Provides Page and PageSize with sensible defaults.
/// </summary>
public abstract record PagedQuery<TResponse>(
	int Page = 1,
	int PageSize = 20
) : Abstractions.IQuery<PagedResponse<TResponse>>;
