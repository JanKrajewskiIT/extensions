using Extensions.Domain.Primitives;

namespace Extensions.Application.Commands;

/// <summary>Trusted execution identity supplied by the host, never inferred from request property names.</summary>
public sealed record CommandContext
{
    /// <summary>Maximum length of a persisted owner, actor, partition or versioned operation key.</summary>
    public const int MaximumKeyLength = 200;

    /// <summary>Creates a context after host authorization; operation names must include their contract version.</summary>
    public CommandContext(Guid commandId, string owner, string actor, string partition, string operation,
        TraceContext? trace = null)
    {
        CommandId = DomainGuard.Identity(commandId, nameof(commandId));
        Owner = Key(owner, nameof(owner));
        Actor = Key(actor, nameof(actor));
        Partition = Key(partition, nameof(partition));
        Operation = Key(operation, nameof(operation));
        Trace = trace;
    }

    /// <summary>Idempotency identity retained across retries.</summary>
    public Guid CommandId { get; }

    /// <summary>Authorized data owner, which may differ from the acting reviewer or workload.</summary>
    public string Owner { get; }

    /// <summary>Authenticated or explicitly delegated actor.</summary>
    public string Actor { get; }

    /// <summary>Host-owned scope or tenant partition of this command.</summary>
    public string Partition { get; }

    /// <summary>Stable versioned operation contract, independent of assembly versions.</summary>
    public string Operation { get; }

    /// <summary>Optional caller correlation and causation.</summary>
    public TraceContext? Trace { get; }

    private static string Key(string value, string name)
    {
        DomainGuard.Required(value, name);
        if (value.Length > MaximumKeyLength)
        {
            throw new ArgumentOutOfRangeException(name, $"A command context key cannot exceed {MaximumKeyLength} characters.");
        }

        return value;
    }
}
