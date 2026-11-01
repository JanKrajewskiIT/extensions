namespace Extensions.Domain.Primitives;

/// <summary>Carries correlation and optional causation identities across boundaries.</summary>
public sealed record TraceContext
{
    /// <summary>Rejects empty supplied identities without generating replacements.</summary>
    public TraceContext(Guid correlationId, Guid? causationId)
    {
        CorrelationId = DomainGuard.Identity(correlationId, nameof(correlationId));
        CausationId = causationId is null ? null : DomainGuard.Identity(causationId.Value, nameof(causationId));
    }

    /// <summary>Identity shared by related work.</summary>
    public Guid CorrelationId { get; }
    /// <summary>Immediate command/event cause, if known.</summary>
    public Guid? CausationId { get; }
}
