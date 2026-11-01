namespace Extensions.Messaging;

public interface IMessageProcessingBudget : IDisposable
{
	ValueTask TakeAttemptAsync(CancellationToken cancellationToken);
}
