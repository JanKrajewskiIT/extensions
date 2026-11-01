namespace Extensions.Infrastructure.Http;

/// <summary>HTTP result observation, distinct from transactional commit evidence.</summary>
public enum MutationAuditOutcome
{
	/// <summary>The endpoint returned a successful result.</summary>
	Succeeded,
	/// <summary>The endpoint returned an HTTP refusal.</summary>
	Denied,
	/// <summary>The endpoint raised an exception.</summary>
	Failed,
}
