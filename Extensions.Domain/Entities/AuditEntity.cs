namespace Extensions.Domain.Entities;

/// <summary>
/// Abstract entity adding audit trail fields (created/modified by/on).
/// All audit fields are set automatically by the AuditInterceptor via EF Core change tracker.
/// </summary>
public abstract class AuditEntity : BaseEntity
{
	public Guid CreatedBy { get; set; }

	public DateTimeOffset CreatedOn { get; set; }

	public Guid ModifiedBy { get; set; }

	public DateTimeOffset ModifiedOn { get; set; }
}
