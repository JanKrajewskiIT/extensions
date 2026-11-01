namespace Extensions.Domain.Exceptions;

/// <summary>Exposes a stable machine code without using an exception message as a public contract.</summary>
public interface ICodedException
{
    /// <summary>Stable application-owned machine code.</summary>
    string Code { get; }

    /// <summary>Category used by the application's error mapping.</summary>
    FailureKind Kind { get; }
}
