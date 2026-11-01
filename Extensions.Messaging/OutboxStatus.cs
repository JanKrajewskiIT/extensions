namespace Extensions.Messaging;

public enum OutboxStatus
{
	Pending,
	Publishing,
	Published,
	Quarantined,
}
