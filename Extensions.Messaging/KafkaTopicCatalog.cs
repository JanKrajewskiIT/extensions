using System.Collections.Immutable;

namespace Extensions.Messaging;

public sealed class KafkaTopicCatalog
{
	private readonly object gate = new();
	private readonly HashSet<string> names = new(StringComparer.Ordinal);
	private const int MaximumTopics = 128;
	private const int MaximumTopicLength = 249;

	public void Register(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumTopicLength || name is "." or ".." ||
			name.Any(value => !char.IsAsciiLetterOrDigit(value) && value is not ('.' or '_' or '-')))
		{
			throw new ArgumentException("A topic requires a bounded ASCII name.", nameof(name));
		}

		lock (gate)
		{
			if (names.Count >= MaximumTopics && !names.Contains(name))
			{
				throw new InvalidOperationException("The topic catalog exceeds its registration bound.");
			}

			names.Add(name);
		}
	}

	public ImmutableArray<string> Snapshot()
	{
		lock (gate)
		{
			return [.. names.Order(StringComparer.Ordinal)];
		}
	}
}
