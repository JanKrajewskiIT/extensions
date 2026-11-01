using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Extensions.Infrastructure.Database;

/// <summary>Bounded, ordered write-time observation checks over the caller's local database transaction.</summary>
public sealed class TransactionalReadSet<TIdentity>(int maximum, TimeProvider clock,
	Func<Exception> capacityFailure, Func<Exception> changedFailure, Func<Exception> expiredFailure) where TIdentity : notnull
{
	private readonly Dictionary<TIdentity, DatabaseReadObservation> observations = [];
	private readonly TimeProvider time = clock ?? throw new ArgumentNullException(nameof(clock));
	private readonly Func<Exception> capacityError = capacityFailure ?? throw new ArgumentNullException(nameof(capacityFailure));
	private readonly Func<Exception> changedError = changedFailure ?? throw new ArgumentNullException(nameof(changedFailure));
	private readonly Func<Exception> expiredError = expiredFailure ?? throw new ArgumentNullException(nameof(expiredFailure));
	private readonly int capacity = maximum > 0 ? maximum : throw new ArgumentOutOfRangeException(nameof(maximum));

	/// <summary>Whether any observations require a transactional write check.</summary>
	public bool HasObservations => observations.Count > 0;

	/// <summary>Preserves strict legacy capacity checking before every observation, including replacement.</summary>
	public void Remember(TIdentity identity, DatabaseReadObservation observation)
	{
		ArgumentNullException.ThrowIfNull(observation);
		if (observations.Count >= capacity)
		{
			throw capacityError();
		}

		Set(identity, observation);
	}

	/// <summary>Bounds new identities and retains the earliest expiry when the same revision is observed again.</summary>
	public void RememberEarliest(TIdentity identity, DatabaseReadObservation observation)
	{
		ArgumentNullException.ThrowIfNull(observation);
		observations.TryGetValue(identity, out var prior);
		if (prior is null && observations.Count >= capacity)
		{
			throw capacityError();
		}

		Set(identity, prior is not null && prior.ExpiresAt < observation.ExpiresAt
			? observation with { ExpiresAt = prior.ExpiresAt } : observation);
	}

	private void Set(TIdentity identity, DatabaseReadObservation observation)
	{
		if (observations.TryGetValue(identity, out var prior) && prior.Revision != observation.Revision)
		{
			throw changedError();
		}

		observations[identity] = observation;
	}

	/// <summary>Checks only observations belonging to this connection, in stable ordinal component order.</summary>
	public async ValueTask RequireAsync(DbContext context, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (!HasObservations)
		{
			return;
		}

		var current = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
		foreach (var observation in observations.Values.OrderBy(value => value.Order, OrderComparer.Instance))
		{
			if (!new NpgsqlConnectionStringBuilder(observation.Connection).EquivalentTo(current))
			{
				continue;
			}

			if (observation.ExpiresAt <= time.GetUtcNow())
			{
				throw expiredError();
			}

			await observation.Validate(context, token);
		}
	}

	private sealed class OrderComparer : IComparer<ImmutableArray<string>>
	{
		internal static OrderComparer Instance { get; } = new();

		public int Compare(ImmutableArray<string> left, ImmutableArray<string> right)
		{
			for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
			{
				var order = StringComparer.Ordinal.Compare(left[index], right[index]);
				if (order != 0)
				{
					return order;
				}
			}

			return left.Length.CompareTo(right.Length);
		}
	}
}
