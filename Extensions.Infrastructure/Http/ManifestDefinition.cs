namespace Extensions.Infrastructure.Http;

public sealed class ManifestDefinition<TReference> where TReference : class
{
	private ManifestDefinition(string owner, string kind, Type documentType,
		Func<object, TReference> reference, Action<object> validate)
	{
		Owner = owner;
		Kind = kind;
		DocumentType = documentType;
		Reference = reference;
		Validate = validate;
	}

	public string Owner { get; }
	public string Kind { get; }
	public Type DocumentType { get; }
	public Func<object, TReference> Reference { get; }
	public Action<object> Validate { get; }

	public static ManifestDefinition<TReference> Create<TDocument>(string owner, string kind,
		Func<TDocument, TReference> reference, Action<TDocument> validate) where TDocument : notnull
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(owner);
		ArgumentException.ThrowIfNullOrWhiteSpace(kind);
		ArgumentNullException.ThrowIfNull(reference);
		ArgumentNullException.ThrowIfNull(validate);
		return new ManifestDefinition<TReference>(owner, kind, typeof(TDocument),
			value => reference((TDocument)value), value => validate((TDocument)value));
	}
}
