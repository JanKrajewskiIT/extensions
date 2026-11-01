using Extensions.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Commands;

internal sealed class PipelineResponseCommandHandler<TContext, TCommand, TResponse>(
    CommandPipeline<TContext, TCommand, TResponse> pipeline) : ICommandHandler<TCommand, TResponse>
    where TContext : DbContext where TCommand : ICommand<TResponse>
{
    public Task<TResponse> ExecuteAsync(TCommand command, CancellationToken cancellationToken = default) =>
        pipeline.ExecuteAsync(command, cancellationToken);
}
