namespace Extensions.Integrations;

public sealed class BoundedTtlCache<TKey, TValue> where TKey : notnull
{
	private readonly object sync = new();

	private readonly Dictionary<TKey, LinkedListNode<Entry>> entries = [];

	private readonly LinkedList<Entry> recency = [];

	private readonly TimeProvider clock;

	private readonly int maximumEntries;

	public BoundedTtlCache(TimeProvider clock, int maximumEntries = 10000)
	{
		this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
		if (maximumEntries is < 1 or > 1000000)
		{
			throw new ArgumentOutOfRangeException(nameof(maximumEntries));
		}

		this.maximumEntries = maximumEntries;
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		ArgumentNullException.ThrowIfNull(key);
		lock (sync)
		{
			if (!entries.TryGetValue(key, out var node))
			{
				value = default!;
				return false;
			}

			if (node.Value.ExpiresAt <= clock.GetUtcNow())
			{
				Remove(node);
				value = default!;
				return false;
			}

			recency.Remove(node);
			recency.AddFirst(node);
			value = node.Value.Value;
			return true;
		}
	}

	public void Set(TKey key, TValue value, TimeSpan timeToLive)
	{
		ArgumentNullException.ThrowIfNull(key);
		if (timeToLive <= TimeSpan.Zero || timeToLive > TimeSpan.FromDays(365))
		{
			throw new ArgumentOutOfRangeException(nameof(timeToLive));
		}

		lock (sync)
		{
			if (entries.TryGetValue(key, out var existing))
			{
				existing.Value = new Entry(key, value, clock.GetUtcNow() + timeToLive);
				recency.Remove(existing);
				recency.AddFirst(existing);
				return;
			}

			while (entries.Count >= maximumEntries)
			{
				var oldest = recency.Last ?? throw new InvalidOperationException("The bounded cache index is inconsistent.");
				Remove(oldest);
			}

			var node = recency.AddFirst(new Entry(key, value, clock.GetUtcNow() + timeToLive));
			entries.Add(key, node);
		}
	}

	public bool Remove(TKey key)
	{
		ArgumentNullException.ThrowIfNull(key);
		lock (sync)
		{
			if (!entries.TryGetValue(key, out var node))
			{
				return false;
			}

			Remove(node);
			return true;
		}
	}

	private void Remove(LinkedListNode<Entry> node)
	{
		recency.Remove(node);
		entries.Remove(node.Value.Key);
	}

	private sealed record Entry(TKey Key, TValue Value, DateTimeOffset ExpiresAt);
}
