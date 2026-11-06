using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Extensions.Integrations;

public static class JsonLines
{
	public static async IAsyncEnumerable<JsonElement> ReadAsync(Stream stream, int maximumLineBytes,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLineBytes);
		var buffer = new byte[8192];
		using var line = new MemoryStream();
		int count;
		while ((count = await stream.ReadAsync(buffer, cancellationToken)) != 0)
		{
			for (var index = 0; index < count; index++)
			{
				if (buffer[index] == '\n')
				{
					if (line.Length > 0)
					{
						using var document = JsonDocument.Parse(line.ToArray());
						yield return document.RootElement.Clone();
						line.SetLength(0);
					}

					continue;
				}

				if (line.Length >= maximumLineBytes)
				{
					throw new InvalidDataException("Provider stream line exceeds the configured limit.");
				}

				line.WriteByte(buffer[index]);
			}
		}

		if (line.Length > 0)
		{
			throw new EndOfStreamException("The provider stream ended inside a record.");
		}
	}
}
