namespace Extensions.Infrastructure.Commands;

/// <summary>Service-local immutable outcome committed with the handler's writes; not an HTTP response DTO.</summary>
public sealed class CommandReceipt
{
    /// <summary>Deterministic identity of owner, partition, operation and CommandId.</summary>
    public Guid Id { get; init; }

    /// <summary>Original caller command identity.</summary>
    public Guid CommandId { get; init; }

    /// <summary>Authorized data owner.</summary>
    public required string Owner { get; init; }

    /// <summary>Host-owned tenant/scope partition.</summary>
    public required string Partition { get; init; }

    /// <summary>Versioned operation contract.</summary>
    public required string Operation { get; init; }

    /// <summary>Canonical command hash used to reject identity reuse.</summary>
    public required string RequestHash { get; init; }

    /// <summary>Canonical result; access and retention are owned by the service.</summary>
    public required string ResultJson { get; init; }

    /// <summary>Integrity hash checked before replaying the result.</summary>
    public required string ResultHash { get; init; }

    /// <summary>UTC completion time of the successful attempt.</summary>
    public DateTimeOffset CompletedAt { get; init; }
}
