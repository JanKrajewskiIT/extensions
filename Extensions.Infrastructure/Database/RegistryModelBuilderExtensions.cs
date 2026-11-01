using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Opt-in registry mapping over an owner-owned row, without requiring BaseEntity or AuditEntity.</summary>
public static class RegistryModelBuilderExtensions
{
	/// <summary>Maps the immutable registry key and indexes; the host owns schema and historical migrations.</summary>
	public static ModelBuilder AddDocumentRegistry<TRow>(this ModelBuilder modelBuilder,
		RegistryColumnNames columns, string table = "registry") where TRow : class
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);
		ArgumentNullException.ThrowIfNull(columns);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);
		var documents = modelBuilder.Entity<TRow>();
		documents.ToTable(table);
		documents.HasKey(columns.Owner, columns.Kind, columns.Id, columns.Version);
		documents.Property<string>(columns.Kind).HasMaxLength(100);
		documents.Property<string>(columns.Id).HasMaxLength(200);
		documents.Property<string>(columns.Version).HasMaxLength(100);
		documents.Property<string>(columns.ContentHash).HasMaxLength(64);
		documents.Property<string>(columns.Payload).HasColumnType("jsonb");
		documents.HasIndex(columns.Kind, columns.ResourceId, columns.AvailableAt);
		documents.HasIndex(columns.Kind, columns.ScopeKey, columns.PublishedAt);
		documents.HasIndex(columns.Owner, columns.Kind, columns.PublishedAt, columns.Id, columns.Version);
		return modelBuilder;
	}
}
