using System.Linq.Expressions;
using System.Reflection;
using Extensions.Domain.Abstractions;
using Extensions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Base DbContext with shared fenced writes; the current-user constructor applies query filters
/// on all entities inheriting from <see cref="AuditEntity"/>.
/// Derive your service-specific ApplicationDbContext from this class.
/// </summary>
public abstract class BaseApplicationDbContext : DbContext
{
	private readonly ICurrentUserService? _currentUserService;
	private readonly DatabaseWritePipeline writePipeline = new();

	/// <summary>
	/// Trusted query owner; a delegated reader may override it independently of the audit actor.
	/// </summary>
	protected virtual Guid QueryOwnerId => _currentUserService?.UserId ?? throw new UnauthorizedAccessException("Current user is required for scoped queries.");

	/// <summary>Initializes the context with the given <paramref name="options"/> and <paramref name="currentUserService"/> for user-scoping.</summary>
	protected BaseApplicationDbContext(
		DbContextOptions options,
		ICurrentUserService currentUserService) : base(options)
	{
		ArgumentNullException.ThrowIfNull(currentUserService);
		_currentUserService = currentUserService;
	}

	/// <summary>Creates an owner-managed context with explicit model and ownership rules.</summary>
	protected BaseApplicationDbContext(DbContextOptions options) : base(options)
	{
	}

	/// <summary>Supplies owner admissions, transactional observations and local critical audit.</summary>
	protected virtual DatabaseSavePlan CreateSavePlan() => DatabaseSavePlan.Empty;

	/// <inheritdoc />
	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		DatabaseWritePipeline.RequireSynchronousSave(this, CreateSavePlan());
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	/// <inheritdoc />
	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
		writePipeline.SaveAsync(this, CreateSavePlan(), PersistAsync,
			acceptAllChangesOnSuccess, cancellationToken);

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		if (_currentUserService is not null)
		{
			ApplyUserScopingFilters(modelBuilder);
			modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
		}

		ConfigureModel(modelBuilder);
	}

	/// <summary>
	/// Override to add service-specific model configuration (enums, seed data, etc.).
	/// Called after query filters and entity configurations are applied.
	/// </summary>
	protected virtual void ConfigureModel(ModelBuilder modelBuilder)
	{
	}

	private Task<int> PersistAsync(bool acceptAllChangesOnSuccess, CancellationToken token) =>
		base.SaveChangesAsync(acceptAllChangesOnSuccess, token);

	private void ApplyUserScopingFilters(ModelBuilder modelBuilder)
	{
		var currentUserIdProperty = typeof(BaseApplicationDbContext)
			.GetProperty(nameof(QueryOwnerId), BindingFlags.NonPublic | BindingFlags.Instance)!;

		var thisAsBase = Expression.Convert(Expression.Constant(this), typeof(BaseApplicationDbContext));

		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (!entityType.ClrType.IsAssignableTo(typeof(AuditEntity)))
			{
				continue;
			}

			var parameter = Expression.Parameter(entityType.ClrType, "e");
			var createdBy = Expression.Property(parameter, nameof(AuditEntity.CreatedBy));
			var currentUserId = Expression.Property(thisAsBase, currentUserIdProperty);
			var equals = Expression.Equal(createdBy, currentUserId);
			var lambda = Expression.Lambda(equals, parameter);

			entityType.SetQueryFilter(lambda);
		}
	}
}
