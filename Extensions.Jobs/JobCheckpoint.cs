using Extensions.Domain.Primitives;

namespace Extensions.Jobs;

public record JobCheckpoint<TArtifact>(TArtifact State, string Cursor, DateTimeOffset SavedAt) : IJobCheckpoint
{
	public void Validate()
	{
		ArgumentNullException.ThrowIfNull(State);
		DomainGuard.Required(Cursor, nameof(Cursor));
	}
}
