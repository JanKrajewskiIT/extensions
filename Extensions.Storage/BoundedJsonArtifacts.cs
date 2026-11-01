using System.Security.Cryptography;
using System.Text;
using Extensions.Domain.Primitives;
using Extensions.Domain.Exceptions;

namespace Extensions.Storage;

/// <summary>Bounded UTF-8 artifact reading with independent manifest length and SHA-256 validation.</summary>
public static class BoundedJsonArtifacts
{
	/// <summary>Reads valid UTF-8 within the caller budget and verifies the complete immutable payload.</summary>
	public static async ValueTask<string> ReadTextAsync(IVersionedArtifactStore store, ArtifactReference artifact,
		long maximumBytes, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(artifact);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
		if (artifact.MediaType != "application/json" || artifact.Length < 1 || artifact.Length > maximumBytes)
		{
			throw new BusinessRuleException("artifact.json_read_budget_exceeded");
		}

		await using var source = await store.OpenReadAsync(artifact, token);
		using var buffer = new MemoryStream();
		var block = new byte[65536];
		int count;
		while ((count = await source.ReadAsync(block, token)) > 0)
		{
			if (buffer.Length + count > artifact.Length)
			{
				throw new InvalidDataException("The artifact exceeds its immutable length.");
			}

			await buffer.WriteAsync(block.AsMemory(0, count), token);
		}

		var bytes = buffer.ToArray();
		if (bytes.LongLength != artifact.Length || new ContentHash(Convert.ToHexString(SHA256.HashData(bytes))) != artifact.ContentHash)
		{
			throw new InvalidDataException("The artifact failed its immutable integrity check.");
		}

		try
		{
			return new UTF8Encoding(false, true).GetString(bytes);
		}
		catch (DecoderFallbackException error)
		{
			throw new InvalidDataException("The artifact is not valid UTF-8 JSON.", error);
		}
	}
}
