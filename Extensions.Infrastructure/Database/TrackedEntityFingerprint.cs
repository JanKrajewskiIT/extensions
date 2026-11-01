using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Extensions.Infrastructure.Database;

/// <summary>Pure EF metadata projection; the owner supplies its canonical hash representation.</summary>
public static class TrackedEntityFingerprint
{
	/// <summary>Fingerprints primary key values, including deleted rows and composite keys.</summary>
	public static string Key(EntityEntry entry, Func<IReadOnlyDictionary<string, object?>, string> fingerprint) =>
		fingerprint(entry.Metadata.FindPrimaryKey()!.Properties.ToDictionary(value => value.Name,
			value => entry.State == EntityState.Deleted ? entry.OriginalValues[value.Name] : entry.CurrentValues[value.Name], StringComparer.Ordinal));

	/// <summary>Fingerprints retained property values without logging payloads.</summary>
	public static string Values(PropertyValues values, Func<IReadOnlyDictionary<string, object?>, string> fingerprint) =>
		fingerprint(values.Properties.ToDictionary(value => value.Name, value => values[value], StringComparer.Ordinal));
}
