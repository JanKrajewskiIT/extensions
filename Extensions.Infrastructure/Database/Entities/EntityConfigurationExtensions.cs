using Extensions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Extensions.Infrastructure.Database.Entities;

/// <summary>
/// Fluent API helpers for common EF Core table configurations.
/// </summary>
public static class EntityConfigurationExtensions
{
    /// <summary>
    /// Maps <typeparamref name="T"/> to <paramref name="tableName"/> and configures
    /// <see cref="BaseEntity.Id"/> as the primary key.
    /// </summary>
    public static void ToBasicTable<T>(this EntityTypeBuilder<T> builder, string tableName) where T : BaseEntity
    {
        builder.ToTable(tableName);
        builder.HasKey(e => e.Id);
    }

    /// <summary>
    /// Maps <typeparamref name="T"/> to <paramref name="tableName"/>, configures
    /// <see cref="BaseEntity.Id"/> as the primary key, and marks all audit columns as required
    /// with server-side <c>CURRENT_TIMESTAMP</c> defaults for the timestamp fields.
    /// </summary>
    public static void ToAuditTable<T>(this EntityTypeBuilder<T> builder, string tableName) where T : AuditEntity
    {
        builder.ToBasicTable(tableName);

        builder.Property(e => e.CreatedBy)
            .IsRequired();

        builder.Property(e => e.CreatedOn)
            .HasDefaultValueSql(PostgresCommandConsts.CurrentTimeStamp)
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(e => e.ModifiedBy)
            .IsRequired();

        builder.Property(e => e.ModifiedOn)
            .HasDefaultValueSql(PostgresCommandConsts.CurrentTimeStamp)
            .ValueGeneratedOnAddOrUpdate()
            .IsRequired();
    }
}
