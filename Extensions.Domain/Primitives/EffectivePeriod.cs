namespace Extensions.Domain.Primitives;

/// <summary>A UTC half-open validity period with an optional exclusive end.</summary>
public sealed record EffectivePeriod
{
    /// <summary>Requires any end to follow the start and normalizes provided endpoints to UTC.</summary>
    public EffectivePeriod(DateTimeOffset from, DateTimeOffset? to)
    {
        if (to <= from)
        {
            throw new ArgumentException("An effective period must end after it starts.", nameof(to));
        }

        From = from.ToUniversalTime();
        To = to?.ToUniversalTime();
    }

    /// <summary>Inclusive UTC start.</summary>
    public DateTimeOffset From { get; }
    /// <summary>Exclusive UTC end, or no upper bound.</summary>
    public DateTimeOffset? To { get; }

    /// <summary>Tests membership using an exclusive end when one is present.</summary>
    public bool Contains(DateTimeOffset timestamp) => timestamp >= From && (To is null || timestamp < To);
}
