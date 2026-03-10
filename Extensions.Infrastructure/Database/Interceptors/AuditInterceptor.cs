using Extensions.Domain.Abstractions;
using Extensions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Extensions.Infrastructure.Database.Interceptors;

/// <summary>
/// EF Core interceptor that automatically stamps audit fields on <see cref="AuditEntity"/> instances
/// during every save operation. Never set audit fields manually — this interceptor handles them.
/// </summary>
public sealed class AuditInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context is not null)
		{
			UpdateAuditFields(eventData.Context);
		}

		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	public override InterceptionResult<int> SavingChanges(
		DbContextEventData eventData,
		InterceptionResult<int> result)
	{
		if (eventData.Context is not null)
		{
			UpdateAuditFields(eventData.Context);
		}

		return base.SavingChanges(eventData, result);
	}

	private void UpdateAuditFields(DbContext context)
	{
		var userId = currentUserService.UserId ?? Guid.Empty;

		var now = DateTimeOffset.UtcNow;

		foreach (var entry in context.ChangeTracker.Entries<AuditEntity>())
		{
			switch (entry.State)
			{
				case EntityState.Added:
					entry.Property(nameof(AuditEntity.CreatedBy)).CurrentValue = userId;
					entry.Property(nameof(AuditEntity.CreatedOn)).CurrentValue = now;
					entry.Property(nameof(AuditEntity.ModifiedBy)).CurrentValue = userId;
					entry.Property(nameof(AuditEntity.ModifiedOn)).CurrentValue = now;
					break;

				case EntityState.Modified:
					entry.Property(nameof(AuditEntity.ModifiedBy)).CurrentValue = userId;
					entry.Property(nameof(AuditEntity.ModifiedOn)).CurrentValue = now;
					break;
			}
		}
	}
}
