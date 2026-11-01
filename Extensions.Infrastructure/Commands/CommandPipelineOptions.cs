namespace Extensions.Infrastructure.Commands;

/// <summary>Bounds the JSON contracts persisted by the opt-in transactional command pipeline.</summary>
public sealed class CommandPipelineOptions
{
    /// <summary>Default bound for each canonical request and response.</summary>
    public const int DefaultMaximumPayloadBytes = 1024 * 1024;

    /// <summary>Maximum UTF-8 size of the canonical command snapshot.</summary>
    public int MaximumRequestBytes { get; set; } = DefaultMaximumPayloadBytes;

    /// <summary>Maximum UTF-8 size of the result retained for idempotent reads.</summary>
    public int MaximumResponseBytes { get; set; } = DefaultMaximumPayloadBytes;
}
