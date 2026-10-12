namespace Extensions.Infrastructure.Security;

/// <summary>
/// Configuration options for token encryption at rest.
/// Bind to the <c>"TokenEncryption"</c> configuration section.
/// </summary>
public record TokenEncryptionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TokenEncryption";

    /// <summary>
    /// Base64-encoded 256-bit (32-byte) AES key used to encrypt/decrypt tokens at rest.
    /// Generate with: <c>Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))</c>
    /// </summary>
    public required string Key { get; init; }
}
