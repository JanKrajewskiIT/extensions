using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>An owner-supplied local observation; its callback owns the concrete model check.</summary>
public sealed record DatabaseReadObservation
{
	/// <summary>Creates a bounded-lifetime observation; connection details are never diagnostic output.</summary>
	public DatabaseReadObservation(string connection, ImmutableArray<string> order, string revision,
		DateTimeOffset expiresAt, Func<DbContext, CancellationToken, ValueTask> validate)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connection);
		ArgumentNullException.ThrowIfNull(revision);
		ArgumentNullException.ThrowIfNull(validate);
		if (order.IsDefault)
		{
			throw new ArgumentException("Observation ordering components are required.", nameof(order));
		}

		Connection = connection;
		Order = order;
		Revision = revision;
		ExpiresAt = expiresAt;
		Validate = validate;
	}

	internal string Connection { get; }
	internal Func<DbContext, CancellationToken, ValueTask> Validate { get; }

	/// <summary>Stable ordinal ordering components supplied by the owner.</summary>
	public ImmutableArray<string> Order { get; }

	/// <summary>Opaque expected revision or hash.</summary>
	public string Revision { get; }

	/// <summary>Last permitted observation use.</summary>
	public DateTimeOffset ExpiresAt { get; init; }

	/// <inheritdoc />
	public override string ToString() => nameof(DatabaseReadObservation);
}
