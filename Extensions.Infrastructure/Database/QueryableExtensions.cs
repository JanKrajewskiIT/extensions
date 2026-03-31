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
	/// When pageSize is 0 or less, all records are returned without pagination.
	/// </summary>
	public static async Task<PagedResponse<T>> ToPagedAsync<T>(
		this IQueryable<T> query,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var resolvedPage = page > 0 ? page : 1;
		var resolvedPageSize = pageSize > 0 ? pageSize : 0;
		var totalCount = await query.CountAsync(cancellationToken);

		List<T> items;

		if (resolvedPageSize > 0)
		{
			items = await query
				.Skip((resolvedPage - 1) * resolvedPageSize)
				.Take(resolvedPageSize)
				.ToListAsync(cancellationToken);
		}
		else
		{
			items = await query.ToListAsync(cancellationToken);
		}

		return new PagedResponse<T>(items, totalCount, resolvedPage, resolvedPageSize);
	}
}
