using Extensions.Domain.Primitives;

namespace Extensions.Domain.Exceptions;

/// <summary>A structural input refusal distinct from a business or concurrency conflict.</summary>
public sealed class RequestValidationException(string code, Exception? innerException = null)
    : ArgumentException(DomainGuard.Required(code, nameof(code)), innerException), ICodedException
{
    /// <inheritdoc />
    public string Code { get; } = code;

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.Validation;
}
