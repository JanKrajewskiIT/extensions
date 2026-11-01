namespace Extensions.Messaging;

public enum KafkaDeliveryOutcome
{
	Completed,
	Filtered,
	Malformed,
	Rejected,
	AttemptsExhausted,
	BudgetExhausted,
	OwnershipLost,
}
