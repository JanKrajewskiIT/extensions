namespace Extensions.Application.Abstractions;

/// <summary>
/// A generic paginated result wrapper for list queries.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public sealed record PaginatedList<T>(
	IReadOnlyList<T> Items,
	int TotalCount,
	int Page,
	int PageSize
)
{
	/// <summary>Total number of pages.</summary>
	public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

	/// <summary>Whether there is a subsequent page.</summary>
	public bool HasNextPage => Page < TotalPages;

	/// <summary>Whether there is a preceding page.</summary>
	public bool HasPreviousPage => Page > 1;
}
