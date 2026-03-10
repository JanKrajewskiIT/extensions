namespace Extensions.Infrastructure.Database.Entities;

/// <summary>
/// Shared constants for database column sizes and validator max lengths.
/// </summary>
public static class DataTypeConsts
{
	/// <summary>Max length for hex color codes (e.g. <c>#FF00AA</c>).</summary>
	public const int HexColorLength = 7;

	/// <summary>Max length for short names, labels, and tags.</summary>
	public const int ShortNameLength = 50;

	/// <summary>Max length for standard names and titles.</summary>
	public const int NameLength = 100;

	/// <summary>Max length for medium-length names and descriptions.</summary>
	public const int MediumNameLength = 200;

	/// <summary>Max length for URLs.</summary>
	public const int UrlLength = 500;

	/// <summary>Max length for long text content and descriptions.</summary>
	public const int TextLength = 5000;
}
