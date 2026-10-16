namespace Extensions.Infrastructure.Ai;

/// <summary>A local AI deadline elapsed. Caller cancellation remains an OperationCanceledException.</summary>
public sealed class AiRequestTimeoutException(Exception cause)
    : TimeoutException("AI operation exceeded its deadline.", cause)
{
}
