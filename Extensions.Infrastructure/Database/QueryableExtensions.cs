using Extensions.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Extension methods for IQueryable to support pagination.
/// </summary>
public static class QueryableExtensions
{
	/// <summary>
	/// Applies pagination to the query and returns a <see cref="PagedResponse{T}"/>.
	/// Executes a COUNT query and a data query in sequence.
	/// </summary>
	public static async Task<PagedResponse<T>> ToPagedAsync<T>(
		this IQueryable<T> query,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var totalCount = await query.CountAsync(cancellationToken);

		var items = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResponse<T>(items, totalCount, page, pageSize);
	}
}
