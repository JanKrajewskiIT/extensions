using Extensions.Domain.Primitives;

namespace Extensions.Domain.Exceptions;

/// <summary>A dependency failure with explicit retry metadata and no dependency response body.</summary>
public sealed class DependencyFailureException : Exception, ICodedException
{
    /// <summary>Creates failure metadata; a retry delay is valid only for a retryable failure.</summary>
    public DependencyFailureException(string code, bool retryable, TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(DomainGuard.Required(code, nameof(code)), innerException)
    {
        if (retryAfter < TimeSpan.Zero || retryAfter is not null && !retryable)
        {
            throw new ArgumentException("A retry delay requires a retryable failure and a nonnegative duration.", nameof(retryAfter));
        }

        Code = code;
        Retryable = retryable;
        RetryAfter = retryAfter;
    }

    /// <inheritdoc />
    public string Code { get; }

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.Dependency;

    /// <summary>Whether retry is permitted by the classified dependency outcome.</summary>
    public bool Retryable { get; }

    /// <summary>Optional delay; it does not override a caller deadline or idempotency policy.</summary>
    public TimeSpan? RetryAfter { get; }
}
