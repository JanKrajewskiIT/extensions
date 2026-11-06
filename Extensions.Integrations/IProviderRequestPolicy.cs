namespace Extensions.Integrations;

/// <summary>The host resolves its own priority vocabulary and canonical policy identity.</summary>
public interface IProviderRequestPolicy
{
	ProviderRequestPolicy Resolve(string provider);
}
