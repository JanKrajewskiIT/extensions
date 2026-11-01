namespace Extensions.Infrastructure.Database;

/// <summary>Trusted audit actor identity, independent of a financial owner or query scope.</summary>
public interface IAuditActorProvider
{
	/// <summary>The acting principal; null retains the legacy system/absent actor convention.</summary>
	Guid? ActorId { get; }
}
