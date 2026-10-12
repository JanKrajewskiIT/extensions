namespace Extensions.Domain.Entities;

/// <summary>
/// Abstract entity adding audit trail fields (created/modified by/on).
/// All audit fields are set automatically by the AuditInterceptor via EF Core change tracker.
/// </summary>
public abstract class AuditEntity : BaseEntity
{
    /// <summary>Gets or sets the identifier of the user who created this record.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>Gets or sets the identifier of the user who last modified this record.</summary>
    public Guid ModifiedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was last modified.</summary>
    public DateTimeOffset ModifiedOn { get; set; }
}
