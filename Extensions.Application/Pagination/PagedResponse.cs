namespace Extensions.Application.Pagination;

/// <summary>
/// Wraps a page of items together with total count and pagination metadata.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    /// <summary>Total number of pages based on TotalCount and PageSize.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Whether there is a next page available.</summary>
    public bool HasNextPage => Page < TotalPages;
}
