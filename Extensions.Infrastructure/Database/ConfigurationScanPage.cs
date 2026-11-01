namespace Extensions.Infrastructure.Database;

/// <summary>A bounded scan page with an owner-supplied cursor shape.</summary>
public sealed record ConfigurationScanPage<TRow, TCursor>(IReadOnlyList<TRow> Items, TCursor? Next) where TCursor : class;
