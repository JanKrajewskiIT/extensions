namespace Extensions.Domain.Entities;

/// <summary>
/// Abstract base entity providing a Guid primary key for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Gets the unique identifier for this entity.</summary>
    public Guid Id { get; init; }
}
