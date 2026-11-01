namespace Extensions.Domain.Primitives;

/// <summary>A nonempty UTC interval whose end is exclusive.</summary>
public sealed record TimeRange
{
    /// <summary>Requires a strictly increasing interval and normalizes both endpoints to UTC.</summary>
    public TimeRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from >= to)
        {
            throw new ArgumentException("The range must be nonempty and increasing.", nameof(to));
        }

        From = from.ToUniversalTime();
        To = to.ToUniversalTime();
    }

    /// <summary>Inclusive UTC start.</summary>
    public DateTimeOffset From { get; }
    /// <summary>Exclusive UTC end.</summary>
    public DateTimeOffset To { get; }

    /// <summary>Tests membership without including the end boundary.</summary>
    public bool Contains(DateTimeOffset timestamp) => timestamp >= From && timestamp < To;

    /// <summary>Adjacent intervals sharing only an endpoint do not overlap.</summary>
    public bool Overlaps(TimeRange other) => From < other.To && other.From < To;
}
