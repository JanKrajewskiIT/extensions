using Extensions.Domain.Primitives;

namespace Extensions.Domain.Exceptions;

/// <summary>A conflict with an expected version or an existing operation identity.</summary>
public sealed class ConcurrencyConflictException(string code, Exception? innerException = null)
    : InvalidOperationException(DomainGuard.Required(code, nameof(code)), innerException), ICodedException
{
    /// <inheritdoc />
    public string Code { get; } = code;

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.Conflict;
}
