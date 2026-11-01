using Extensions.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Commands;

internal sealed class PipelineCommandHandler<TContext, TCommand>(CommandPipeline<TContext, TCommand, CommandUnit> pipeline)
    : ICommandHandler<TCommand> where TContext : DbContext where TCommand : ICommand
{
    public Task ExecuteAsync(TCommand command, CancellationToken cancellationToken = default) =>
        pipeline.ExecuteAsync(command, cancellationToken);
}
