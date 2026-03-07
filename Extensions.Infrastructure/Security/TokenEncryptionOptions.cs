namespace Extensions.Infrastructure.Security;

/// <summary>
/// Configuration options for token encryption at rest.
/// Bind to the "TokenEncryption" configuration section.
/// </summary>
public sealed class TokenEncryptionOptions
{
	public const string SectionName = "TokenEncryption";

	/// <summary>
	/// Base64-encoded 256-bit (32-byte) AES key used to encrypt/decrypt tokens at rest.
	/// Generate with: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
	/// </summary>
	public required string Key { get; init; }
}
