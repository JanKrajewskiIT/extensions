namespace Extensions.Infrastructure.Http;

public interface IManifestDefinitionProvider<TReference> where TReference : class
{
	IReadOnlyCollection<ManifestDefinition<TReference>> Definitions { get; }
}
