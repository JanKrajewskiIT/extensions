namespace Extensions.Domain.Primitives;

/// <summary>Validates structural values without applying application policy.</summary>
public static class DomainGuard
{
    /// <summary>Rejects null, empty or whitespace text without trimming accepted values.</summary>
    public static string Required(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value;
    }

    /// <summary>Rejects an empty identity.</summary>
    public static Guid Identity(Guid value, string parameter)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An identity must not be empty.", parameter);
        }

        return value;
    }

    /// <summary>Accepts a strictly positive decimal without rounding it.</summary>
    public static decimal Positive(decimal value, string parameter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, parameter);
        return value;
    }

    /// <summary>Accepts a strictly positive count.</summary>
    public static int Positive(int value, string parameter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, parameter);
        return value;
    }

    /// <summary>Accepts zero or a positive decimal without rounding it.</summary>
    public static decimal NonNegative(decimal value, string parameter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameter);
        return value;
    }

    /// <summary>Rejects enum values that are not explicitly declared.</summary>
    public static T Defined<T>(T value, string parameter)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameter, value, "Unknown value.");
        }

        return value;
    }

    /// <summary>Accepts a fraction in the closed interval from zero to one.</summary>
    public static decimal Fraction(decimal value, string parameter)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameter, value, "Expected a fraction between zero and one.");
        }

        return value;
    }
}
