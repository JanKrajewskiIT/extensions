namespace Extensions.Domain.Exceptions;

/// <summary>Transport-independent failure categories; applications choose their public mapping.</summary>
public enum FailureKind
{
    /// <summary>Structurally invalid input.</summary>
    Validation,
    /// <summary>Resource unavailable to this invocation.</summary>
    NotFound,
    /// <summary>Expected revision or operation identity conflicts.</summary>
    Conflict,
    /// <summary>Deterministic domain-rule refusal.</summary>
    BusinessRule,
    /// <summary>Failure at an external dependency boundary.</summary>
    Dependency,
    /// <summary>Retained data does not satisfy its integrity contract.</summary>
    DataInconsistency,
}
