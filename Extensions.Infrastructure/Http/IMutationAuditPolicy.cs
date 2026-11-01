using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>Application-owned authority, input evidence and critical-command admission.</summary>
public interface IMutationAuditPolicy
{
	/// <summary>Whether this mutation requires the host's stronger authentication policy.</summary>
	bool RequiresStrongAuthentication(HttpContext context);

	/// <summary>Evaluates validated claims using the host's stronger authentication policy.</summary>
	bool IsStronglyAuthenticated(HttpContext context);

	/// <summary>Creates the operation's audit lifetime and execution strategy.</summary>
	ValueTask<IMutationAuditOperation> PrepareAsync(EndpointFilterInvocationContext context, CancellationToken cancellationToken);
}
