using System.Security.Cryptography;
using Amazon.S3.Model;
using Extensions.Domain.Primitives;

namespace Extensions.Storage;

internal sealed class VerifiedArtifactStream(GetObjectResponse response, ContentHash expected) : Stream
{
	private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
	private long observed;
	private bool verified;
	private bool disposed;
	private InvalidDataException? integrityFailure;

	public override bool CanRead => !disposed;
	public override bool CanSeek => false;
	public override bool CanWrite => false;
	public override long Length => response.ContentLength;
	public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

	public override int Read(byte[] buffer, int offset, int count)
	{
		RequireReadable();
		var read = response.ResponseStream.Read(buffer, offset, count);
		Observe(buffer.AsSpan(offset, read), read == 0 && count != 0);
		return read;
	}

	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		RequireReadable();
		var read = await response.ResponseStream.ReadAsync(buffer, cancellationToken);
		Observe(buffer.Span[..read], read == 0 && buffer.Length != 0);
		return read;
	}

	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
	public override void Flush() => throw new NotSupportedException();
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	protected override void Dispose(bool disposing)
	{
		if (disposing && !disposed)
		{
			disposed = true;
			try
			{
				hash.Dispose();
			}
			finally
			{
				response.Dispose();
			}
		}

		base.Dispose(disposing);
	}

	private void RequireReadable()
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (integrityFailure is not null)
		{
			throw integrityFailure;
		}
	}

	private InvalidDataException Fail(string message)
	{
		return integrityFailure ??= new InvalidDataException(message);
	}

	private void Observe(ReadOnlySpan<byte> bytes, bool end)
	{
		if (verified)
		{
			return;
		}

		observed = checked(observed + bytes.Length);
		if (observed > Length)
		{
			throw Fail("Stored artifact exceeds its declared length.");
		}

		hash.AppendData(bytes);
		if (end)
		{
			if (observed != Length || new ContentHash(Convert.ToHexString(hash.GetHashAndReset())) != expected)
			{
				throw Fail("Artifact checksum or length validation failed.");
			}

			verified = true;
		}
	}
}
