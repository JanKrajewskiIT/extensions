namespace Extensions.Infrastructure.Database.Entities;

/// <summary>
/// Shared constants for database column sizes and validator max lengths.
/// </summary>
public static class DataTypeConsts
{
	public const int HexColorLength = 7;
	public const int ShortNameLength = 50;
	public const int NameLength = 100;
	public const int MediumNameLength = 200;
	public const int UrlLength = 500;
	public const int TextLength = 5000;
}
