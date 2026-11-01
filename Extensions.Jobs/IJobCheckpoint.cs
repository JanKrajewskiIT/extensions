namespace Extensions.Jobs;

public interface IJobCheckpoint
{
	string Cursor { get; }
	DateTimeOffset SavedAt { get; }
	void Validate();
}
