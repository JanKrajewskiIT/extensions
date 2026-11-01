using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public static class JobsModelBuilderExtensions
{
	public static ModelBuilder AddJobs(this ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);
		var jobs = modelBuilder.Entity<JobRow>();
		jobs.ToTable("jobs");
		jobs.HasKey(row => row.Id);
		jobs.Property(row => row.Payload).HasColumnType("jsonb");
		jobs.Property(row => row.Version).IsConcurrencyToken();
		jobs.HasIndex(row => new { row.OwnerSubject, row.OwnerModule, row.ScopeKey, row.IdempotencyKey }).IsUnique();
		jobs.HasIndex(row => new { row.OwnerSubject, row.CommandId, row.ScopeKey }).IsUnique();
		jobs.HasIndex(row => new { row.OwnerModule, row.Status, row.NextAttemptAt });
		jobs.HasIndex(row => new { row.OwnerSubject, row.Kind, row.CreatedAt, row.Id })
			.IsDescending(false, false, true, true).HasDatabaseName("IX_jobs_owner_kind_created_id");
		jobs.HasIndex(row => new { row.OwnerSubject, row.Kind, row.ScopeKey, row.Status, row.CreatedAt })
			.HasDatabaseName("IX_jobs_dependency_created");
		var definitions = modelBuilder.Entity<RecurringScheduleDefinitionRow>();
		definitions.ToTable("recurring_schedule_definitions");
		definitions.HasKey(row => new { row.Owner, row.Id, row.Version });
		definitions.Property(row => row.Payload).HasColumnType("jsonb");
		definitions.HasIndex(row => new { row.Owner, row.Id, row.PublishedAt });
		var schedules = modelBuilder.Entity<RecurringScheduleStateRow>();
		schedules.ToTable("recurring_schedule_state");
		schedules.HasKey(row => new { row.Owner, row.ScheduleId });
		schedules.Property(row => row.Version).IsConcurrencyToken();
		schedules.HasIndex(row => new { row.NextRunAt, row.Owner, row.ScheduleId });
		return modelBuilder;
	}
}
