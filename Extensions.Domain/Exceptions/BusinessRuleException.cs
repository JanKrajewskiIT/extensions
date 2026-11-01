using Extensions.Domain.Primitives;

namespace Extensions.Domain.Exceptions;

/// <summary>A deterministic business-rule refusal with a stable code and preserved cause.</summary>
public sealed class BusinessRuleException(string code, Exception? innerException = null)
    : InvalidOperationException(DomainGuard.Required(code, nameof(code)), innerException), ICodedException
{
    /// <inheritdoc />
    public string Code { get; } = code;

    /// <inheritdoc />
    public FailureKind Kind => FailureKind.BusinessRule;
}
