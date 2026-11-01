namespace Extensions.Infrastructure.Commands;

/// <summary>Local command audit committed atomically, without request or response payloads.</summary>
public sealed class CommandAuditEntry
{
    /// <summary>Same deterministic operation identity as its receipt.</summary>
    public Guid Id { get; init; }

    /// <summary>Original caller command identity.</summary>
    public Guid CommandId { get; init; }

    /// <summary>Authorized data owner.</summary>
    public required string Owner { get; init; }

    /// <summary>Authenticated or explicitly delegated actor of the committed attempt.</summary>
    public required string Actor { get; init; }

    /// <summary>Host-owned tenant/scope partition.</summary>
    public required string Partition { get; init; }

    /// <summary>Versioned operation contract.</summary>
    public required string Operation { get; init; }

    /// <summary>Canonical command hash; the payload itself is not audited here.</summary>
    public required string RequestHash { get; init; }

    /// <summary>Optional caller correlation identity.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Optional causal command/event identity.</summary>
    public Guid? CausationId { get; init; }

    /// <summary>UTC time the successful local effect was staged.</summary>
    public DateTimeOffset RecordedAt { get; init; }
}
