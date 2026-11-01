using Extensions.Application.Commands;
using Extensions.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Commands;

/// <summary>Opt-in service-owned persistence mapping; hosts own their schema migration and permissions.</summary>
public static class CommandPipelineModelBuilderExtensions
{
    /// <summary>Adds receipts and local audit to the handler's own context; never changes an existing model implicitly.</summary>
    public static ModelBuilder AddCommandPipeline(this ModelBuilder builder, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var receipts = builder.Entity<CommandReceipt>();
        receipts.ToTable("command_receipts", schema);
        receipts.HasKey(row => row.Id);
        receipts.HasIndex(row => new { row.Owner, row.Partition, row.Operation, row.CommandId }).IsUnique();
        receipts.Property(row => row.Owner).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        receipts.Property(row => row.Partition).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        receipts.Property(row => row.Operation).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        receipts.Property(row => row.RequestHash).HasMaxLength(ContentHash.HexLength).IsRequired();
        receipts.Property(row => row.ResultHash).HasMaxLength(ContentHash.HexLength).IsRequired();
        receipts.Property(row => row.ResultJson).HasColumnType("text").IsRequired();

        var audit = builder.Entity<CommandAuditEntry>();
        audit.ToTable("command_audit", schema);
        audit.HasKey(row => row.Id);
        audit.HasIndex(row => new { row.Owner, row.RecordedAt, row.Id });
        audit.Property(row => row.Owner).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        audit.Property(row => row.Actor).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        audit.Property(row => row.Partition).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        audit.Property(row => row.Operation).HasMaxLength(CommandContext.MaximumKeyLength).IsRequired();
        audit.Property(row => row.RequestHash).HasMaxLength(ContentHash.HexLength).IsRequired();
        return builder;
    }
}
