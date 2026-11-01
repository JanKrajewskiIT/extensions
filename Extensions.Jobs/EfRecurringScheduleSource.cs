using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Extensions.Jobs;

public sealed class EfRecurringScheduleSource<TContext, TSchedule, TLease>(TContext context,
	IRecurringScheduleCodec<TSchedule, TLease> codec, TimeProvider clock, ILogger<EfRecurringScheduleSource<TContext, TSchedule, TLease>> logger)
	where TContext : DbContext where TSchedule : class
{
	public const int MaximumSchedulesPerPass = RecurringScheduleTiming.MaximumSchedulesPerPass;

	public async ValueTask<RecurringSchedulePage<TSchedule>> ReadPageAsync(RecurringScheduleCursor? after, CancellationToken token)
	{
		var now = clock.GetUtcNow();
		var owner = after?.Owner;
		var id = after?.Id;
		using var identifiers = new NpgsqlCommandBuilder();
		var schema = context.Model.GetDefaultSchema();
		var table = schema is null ? "recurring_schedule_definitions" : identifiers.QuoteIdentifier(schema) + ".recurring_schedule_definitions";
		// DISTINCT ON preserves the latest immutable version before applying owner pagination.
		var query = FormattableStringFactory.Create(
			"""SELECT DISTINCT ON ("Owner", "Id") * FROM """ + table +
			""" WHERE "AvailableAt" <= {0} AND "PublishedAt" <= {1}""" +
			""" ORDER BY "Owner", "Id", "PublishedAt" DESC, "Version" DESC""", now, now);
		var candidates = from definition in context.Set<RecurringScheduleDefinitionRow>().FromSql(query).AsNoTracking()
			join state in context.Set<RecurringScheduleStateRow>().AsNoTracking()
				on new { definition.Owner, ScheduleId = definition.Id } equals new { state.Owner, state.ScheduleId } into states
			from state in states.DefaultIfEmpty()
			where definition.RevokedAt == null &&
				(owner == null || definition.Owner.CompareTo(owner) > 0 || definition.Owner == owner && definition.Id.CompareTo(id) > 0) &&
				(state == null || state.NextRunAt <= now && (state.LeaseUntil == null || state.LeaseUntil <= now))
			orderby definition.Owner, definition.Id
			select new { Definition = definition, State = state };
		var rows = await candidates.Take(MaximumSchedulesPerPass).ToArrayAsync(token);
		var next = rows.Length == MaximumSchedulesPerPass ? new RecurringScheduleCursor(rows[^1].Definition.Owner, rows[^1].Definition.Id) : null;
		var schedules = ImmutableArray.CreateBuilder<TSchedule>();
		foreach (var candidate in rows)
		{
			var row = candidate.Definition;
			token.ThrowIfCancellationRequested();
			try
			{
				var definition = codec.Decode(row.Payload);
				codec.Validate(definition);
				var details = codec.Describe(definition);
				if (details.Id != row.Id || details.Version != row.Version || details.ContentHash != row.ContentHash || details.RequestedBy != row.Owner)
				{
					throw new InvalidDataException("A recurring schedule differs from its indexed immutable version.");
				}

				if (details.Enabled && RecurringScheduleTiming.NextRunAt(details, candidate.State) <= now)
				{
					schedules.Add(definition);
				}
			}
			catch (Exception failure) when (failure is System.Text.Json.JsonException or ArgumentException or InvalidDataException)
			{
				logger.LogWarning("Recurring schedule definition rejected ({FailureType})", failure.GetType().Name);
			}
		}

		return new RecurringSchedulePage<TSchedule>(schedules.ToImmutable(), next);
	}
}
