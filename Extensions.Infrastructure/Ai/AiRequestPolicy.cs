namespace Extensions.Infrastructure.Ai;

/// <summary>Immutable limits for one host-defined operation. Use a bounded set of names, never user content.</summary>
public sealed record AiRequestPolicy
{
    /// <summary>Stable telemetry label chosen by the host.</summary>
    public string Operation { get; }

    /// <summary>Maximum aggregate UTF-8 size of message text, not a token count or multimodal payload limit.</summary>
    public int MaxInputBytes { get; }

    /// <summary>Output limit sent to the provider.</summary>
    public int MaxOutputTokens { get; }

    /// <summary>Local waiting deadline; it does not guarantee that the provider stops billing.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Rejects invalid names and limits before a provider call can begin.</summary>
    public AiRequestPolicy(string operation, int maxInputBytes, int maxOutputTokens, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (operation.Length > 64 || operation.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.'))
        {
            throw new ArgumentException("Operation names support up to 64 ASCII letters, digits, underscores, hyphens or dots.", nameof(operation));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputTokens);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout, TimeSpan.FromMilliseconds(uint.MaxValue - 1L));

        Operation = operation;
        MaxInputBytes = maxInputBytes;
        MaxOutputTokens = maxOutputTokens;
        Timeout = timeout;
    }
}
