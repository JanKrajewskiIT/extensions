namespace Extensions.Infrastructure.Http;

/// <summary>Host-owned defaults for the opt-in HTTP pipeline.</summary>
public sealed class HttpPipelineOptions
{
	/// <summary>Default duration; endpoint RequestBudgetLimit metadata takes precedence.</summary>
	public TimeSpan DefaultRequestDuration { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>Machine code emitted when the request deadline cancels work.</summary>
	public string DeadlineCode { get; set; } = "request.deadline_exhausted";
}
