using Extensions.Infrastructure.Database;

namespace Extensions.Jobs;

public sealed class JobRow : IRegistryReferenceWrite
{
	string IRegistryReferenceWrite.ReferenceOwner => OwnerSubject;
	string IRegistryReferenceWrite.ReferencePayload => Payload;
	bool IRegistryReferenceWrite.RequiresDataAdmission => Status is not ((int)JobStatus.Failed) and not ((int)JobStatus.Cancelled);

	public Guid Id { get; set; }
	public Guid CommandId { get; set; }
	public string OwnerModule { get; set; } = "";

	public string Kind { get; set; } = "";

	public string ScopeKey { get; set; } = "";

	public string IdempotencyKey { get; set; } = "";

	public string ConcurrencyKey { get; set; } = "";

	public string OwnerSubject { get; set; } = "";

	public int Status { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset NextAttemptAt { get; set; }
	public DateTimeOffset? LeaseUntil { get; set; }
	public DateTimeOffset Deadline { get; set; }
	public string Payload { get; set; } = "";

	public long Version { get; set; }
}
