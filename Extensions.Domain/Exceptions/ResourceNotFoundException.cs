using Extensions.Domain.Primitives;

namespace Extensions.Domain.Exceptions;

/// <summary>A non-disclosing refusal to resolve an application resource.</summary>
public sealed class ResourceNotFoundException(string code, Exception? innerException = null)
    : KeyNotFoundException(DomainGuard.Required(code, nameof(code)), innerException), ICodedException
{
    /// <inheritdoc />
    public string Code { get; } = code;

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.NotFound;
}
