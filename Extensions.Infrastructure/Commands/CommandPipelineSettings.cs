namespace Extensions.Infrastructure.Commands;

internal sealed record CommandPipelineSettings(int MaximumRequestBytes, int MaximumResponseBytes);
