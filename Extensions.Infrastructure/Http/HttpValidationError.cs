namespace Extensions.Infrastructure.Http;

/// <summary>A public field path and machine code; never a validator's raw message.</summary>
public sealed record HttpValidationError(string Path, string Code);
