namespace Extensions.Messaging;

public enum OffsetRecovery
{
	EarliestRetained,
	ExplicitCheckpoint,
	CertifiedSnapshot,
}
