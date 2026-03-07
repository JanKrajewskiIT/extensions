using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Extensions.Infrastructure.Security;

/// <summary>
/// EF Core value converter that encrypts data on write and decrypts on read using AES-256-GCM.
/// Format: Base64(nonce + ciphertext + tag) where nonce=12 bytes, tag=16 bytes.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
	public EncryptedStringConverter(byte[] key)
		: base(
			plaintext => Encrypt(plaintext, key),
			ciphertext => Decrypt(ciphertext, key))
	{
	}

	private static string Encrypt(string plaintext, byte[] key)
	{
		var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
		var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
		RandomNumberGenerator.Fill(nonce);

		var ciphertext = new byte[plaintextBytes.Length];
		var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

		using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
		aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

		var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
		nonce.CopyTo(result, 0);
		ciphertext.CopyTo(result, nonce.Length);
		tag.CopyTo(result, nonce.Length + ciphertext.Length);

		return Convert.ToBase64String(result);
	}

	private static string Decrypt(string encoded, byte[] key)
	{
		var data = Convert.FromBase64String(encoded);

		const int nonceSize = 12;
		const int tagSize = 16;
		var ciphertextSize = data.Length - nonceSize - tagSize;

		if (ciphertextSize < 0)
			throw new CryptographicException("Invalid encrypted data format.");

		var nonce = data.AsSpan(0, nonceSize);
		var ciphertext = data.AsSpan(nonceSize, ciphertextSize);
		var tag = data.AsSpan(nonceSize + ciphertextSize, tagSize);

		var plaintext = new byte[ciphertextSize];

		using var aes = new AesGcm(key, tagSize);
		aes.Decrypt(nonce, ciphertext, tag, plaintext);

		return System.Text.Encoding.UTF8.GetString(plaintext);
	}
}
