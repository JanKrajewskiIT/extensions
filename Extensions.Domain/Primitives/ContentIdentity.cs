using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Extensions.Domain.Primitives;

/// <summary>Builds deterministic identities using length-prefixed UTF-8 components.</summary>
public static class ContentIdentity
{
    /// <summary>Hashes a domain and length-prefixed components, retaining the first 16 digest bytes as a Guid.</summary>
    public static Guid Create(string domain, params string[] parts)
    {
        DomainGuard.Required(domain, nameof(domain));
        ArgumentNullException.ThrowIfNull(parts);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, domain);
        foreach (var part in parts)
        {
            Append(hash, part);
        }

        return new Guid(hash.GetHashAndReset().AsSpan(0, 16));
    }

    /// <summary>Canonical UTC timestamp component in invariant round-trip format.</summary>
    public static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void Append(IncrementalHash hash, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
