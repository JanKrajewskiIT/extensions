namespace Extensions.Jobs;

public sealed class JobRunnerOptions
{
	public string WorkerId { get; set; } = Environment.MachineName;

	public string[] OwnerModules { get; set; } = [];

	public string[] IncludedKinds { get; set; } = [];

	public string[] ExcludedKinds { get; set; } = [];

	public int Parallelism { get; set; } = 1;

	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

	public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

	public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

	public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(20);

	public static bool IsValid(JobRunnerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.Parallelism is >= 1 and <= 64 && options.LeaseDuration > TimeSpan.Zero &&
			options.RenewalInterval > TimeSpan.Zero && options.RenewalInterval * 3 < options.LeaseDuration &&
			options.PollInterval > TimeSpan.Zero && options.DefaultRetryDelay > TimeSpan.Zero &&
			!string.IsNullOrWhiteSpace(options.WorkerId) &&
			options.OwnerModules is { Length: > 0 } && options.OwnerModules.All(value => !string.IsNullOrWhiteSpace(value)) &&
			options.OwnerModules.Distinct(StringComparer.Ordinal).Count() == options.OwnerModules.Length &&
			options.IncludedKinds is not null && options.ExcludedKinds is not null;
	}
}
