using System.Linq.Expressions;
using System.Reflection;
using Extensions.Domain.Abstractions;
using Extensions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Abstract base DbContext that automatically applies user-scoping query filters
/// on all entities inheriting from <see cref="AuditEntity"/>.
/// Derive your service-specific ApplicationDbContext from this class.
/// </summary>
public abstract class BaseApplicationDbContext : DbContext
{
	private readonly ICurrentUserService _currentUserService;

	/// <summary>
	/// Used exclusively in HasQueryFilter expressions — never expose to derived classes.
	/// </summary>
	private Guid CurrentUserId => _currentUserService.UserId ?? Guid.Empty;

	protected BaseApplicationDbContext(
		DbContextOptions options,
		ICurrentUserService currentUserService) : base(options)
	{
		_currentUserService = currentUserService;
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		ApplyUserScopingFilters(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

		ConfigureModel(modelBuilder);
	}

	/// <summary>
	/// Override to add service-specific model configuration (enums, seed data, etc.).
	/// Called after query filters and entity configurations are applied.
	/// </summary>
	protected virtual void ConfigureModel(ModelBuilder modelBuilder) { }

	private void ApplyUserScopingFilters(ModelBuilder modelBuilder)
	{
		var currentUserIdProperty = typeof(BaseApplicationDbContext)
			.GetProperty(nameof(CurrentUserId), BindingFlags.NonPublic | BindingFlags.Instance)!;

		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (!entityType.ClrType.IsAssignableTo(typeof(AuditEntity)))
				continue;

			var parameter = Expression.Parameter(entityType.ClrType, "e");
			var createdBy = Expression.Property(parameter, nameof(AuditEntity.CreatedBy));
			var currentUserId = Expression.Property(Expression.Constant(this), currentUserIdProperty);
			var equals = Expression.Equal(createdBy, currentUserId);
			var lambda = Expression.Lambda(equals, parameter);

			entityType.SetQueryFilter(lambda);
		}
	}
}
