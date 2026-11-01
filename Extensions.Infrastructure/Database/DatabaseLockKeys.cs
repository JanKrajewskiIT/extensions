namespace Extensions.Infrastructure.Database;

/// <summary>One text-key boundary; existing key bytes are preserved for advisory-lock compatibility.</summary>
public static class DatabaseLockKeys
{
	/// <summary>Validates a complete legacy key without trimming or changing its hash input.</summary>
	public static string FromText(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		return key;
	}

	/// <summary>Builds a namespaced key; the host owns component formats and namespace names.</summary>
	public static string Create(string category, params string[] components)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(category);
		ArgumentNullException.ThrowIfNull(components);
		return FromText(string.Join(':', components.Prepend(category)));
	}
}
