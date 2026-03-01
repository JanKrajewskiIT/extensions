namespace Extensions.Domain.Entities;

/// <summary>
/// Abstract base entity providing a Guid primary key for all domain entities.
/// </summary>
public abstract class BaseEntity
{
	public Guid Id { get; init; }
}
