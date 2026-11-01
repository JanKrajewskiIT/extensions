using Microsoft.EntityFrameworkCore;

namespace Extensions.Messaging;

public static class MessagingModelBuilderExtensions
{
	public static ModelBuilder AddOutboxInbox(this ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);
		var outbox = modelBuilder.Entity<OutboxRow>();
		outbox.ToTable("outbox");
		outbox.HasKey(row => row.Id);
		outbox.Property(row => row.Payload).HasColumnType("jsonb");
		outbox.Property(row => row.Version).IsConcurrencyToken();
		outbox.Property(row => row.StreamKey).HasMaxLength(512);
		outbox.Property(row => row.Topic).HasMaxLength(249);
		outbox.HasIndex(row => new { row.StreamKey, row.StreamSequence }).IsUnique();
		outbox.HasIndex(row => new { row.PublishedAt, row.QuarantinedAt, row.NextAttemptAt });
		var inbox = modelBuilder.Entity<InboxRow>();
		inbox.ToTable("inbox");
		inbox.HasKey(row => new { row.Consumer, row.ScopeKey, row.MessageId });
		inbox.Property(row => row.Consumer).HasMaxLength(200);
		inbox.Property(row => row.ScopeKey).HasMaxLength(200);
		inbox.Property(row => row.PayloadHash).HasMaxLength(64);
		var streams = modelBuilder.Entity<OutboxStreamRow>();
		streams.ToTable("outbox_streams");
		streams.HasKey(row => row.StreamKey);
		streams.Property(row => row.StreamKey).HasMaxLength(512);
		streams.Property(row => row.Sequence).IsConcurrencyToken();
		return modelBuilder;
	}

	public static ModelBuilder AddMessageQuarantine(this ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);
		var quarantine = modelBuilder.Entity<QuarantineRow>();
		quarantine.ToTable("quarantine");
		quarantine.HasKey(row => row.Id);
		quarantine.HasIndex(row => new { row.Consumer, row.Topic, row.Partition, row.Offset }).IsUnique();
		quarantine.Property(row => row.Version).IsConcurrencyToken();
		return modelBuilder;
	}
}
