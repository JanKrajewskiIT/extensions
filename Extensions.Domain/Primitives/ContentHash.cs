namespace Extensions.Domain.Primitives;

/// <summary>An immutable SHA-256 identity normalized to lowercase hexadecimal.</summary>
public sealed record ContentHash
{
    /// <summary>Length of the persisted SHA-256 hexadecimal representation.</summary>
    public const int HexLength = 64;

    /// <summary>Requires exactly 64 hexadecimal characters and normalizes their case.</summary>
    public ContentHash(string sha256)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != HexLength || sha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("Expected a hexadecimal SHA-256 digest.", nameof(sha256));
        }

        Sha256 = sha256.ToLowerInvariant();
    }

    /// <summary>Lowercase hexadecimal SHA-256 digest.</summary>
    public string Sha256 { get; }
}
