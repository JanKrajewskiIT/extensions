namespace Extensions.Infrastructure.Database;

public interface IRegistryReferenceWrite
{
	string ReferenceOwner { get; }
	string ReferencePayload { get; }
	bool RequiresDataAdmission { get; }
}
