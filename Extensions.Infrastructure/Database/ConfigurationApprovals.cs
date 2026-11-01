using System.Collections.Immutable;

namespace Extensions.Infrastructure.Database;

/// <summary>Independent reviewer membership and approval accumulation; role selection stays with the owner.</summary>
public static class ConfigurationApprovals
{
	/// <summary>Requires an assigned principal distinct from the author.</summary>
	public static void RequireReviewer(string owner, string actor, ImmutableArray<string> reviewers)
	{
		if (owner == actor || !reviewers.Contains(actor))
		{
			throw new UnauthorizedAccessException("An assigned independent reviewer and current change are required.");
		}
	}

	/// <summary>Produces the next approval set without changing the host's status enum or stored document.</summary>
	public static ImmutableArray<string> Add(ImmutableArray<string> reviewers, ImmutableArray<string> approvals, string actor)
	{
		if (!reviewers.Contains(actor))
		{
			throw new UnauthorizedAccessException("An assigned reviewer is required.");
		}

		return approvals.Contains(actor) ? approvals : approvals.Add(actor);
	}

	/// <summary>Whether every assigned reviewer has approved.</summary>
	public static bool IsComplete(ImmutableArray<string> reviewers, ImmutableArray<string> approvals) => reviewers.All(approvals.Contains);
}
