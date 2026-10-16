using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.AI;

namespace Extensions.Infrastructure.Ai;

/// <summary>Own as one disposable host singleton. Records usage without storing prompts or response content.</summary>
public sealed class AiRequestExecutor : IDisposable
{
    private readonly Meter _meter;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _calls;
    private readonly Counter<long> _inputTokens;
    private readonly Counter<long> _outputTokens;
    private int _disposed;

    /// <summary>Host-supplied names preserve existing meters and instrument names when adopting this executor.</summary>
    public AiRequestExecutor(string meterName, string metricPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricPrefix);
        _meter = new Meter(meterName);
        _duration = _meter.CreateHistogram<double>($"{metricPrefix}.duration", "s");
        _calls = _meter.CreateCounter<long>($"{metricPrefix}.calls");
        _inputTokens = _meter.CreateCounter<long>($"{metricPrefix}.input_tokens");
        _outputTokens = _meter.CreateCounter<long>($"{metricPrefix}.output_tokens");
    }

    /// <summary>Bounds caller waiting even when the provider ignores cancellation; late results are never returned.</summary>
    public async Task<ChatResponse> GetResponseAsync(IChatClient client, AiRequestPolicy policy,
        IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();
        var chatMessages = messages.ToArray();
        if (chatMessages.Sum(message => (long)Encoding.UTF8.GetByteCount(message.Text)) > policy.MaxInputBytes)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(messages), "AI input exceeds the supported size.")
            ]);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(policy.Timeout);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failure";
        var tags = new TagList { { "operation", policy.Operation } };
        try
        {
            var response = await client.GetResponseAsync(chatMessages,
                new ChatOptions { MaxOutputTokens = policy.MaxOutputTokens }, deadline.Token).WaitAsync(deadline.Token);
            if (response.Usage?.InputTokenCount is long input && input >= 0)
            {
                _inputTokens.Add(input, tags);
            }
            if (response.Usage?.OutputTokenCount is long output && output >= 0)
            {
                _outputTokens.Add(output, tags);
            }
            deadline.Token.ThrowIfCancellationRequested();
            if (response.FinishReason == ChatFinishReason.Length)
            {
                outcome = "output_limit";
                throw new AiOutputLimitException();
            }
            outcome = "success";
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (OperationCanceledException error) when (deadline.IsCancellationRequested)
        {
            outcome = "timeout";
            throw new AiRequestTimeoutException(error);
        }
        finally
        {
            tags.Add("outcome", outcome);
            _calls.Add(1, tags);
            _duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }
    }

    /// <summary>Release after the host's in-flight requests have ended.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _meter.Dispose();
        }
    }
}
