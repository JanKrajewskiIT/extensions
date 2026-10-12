namespace Extensions.Application.Abstractions;

/// <summary>
/// Handler interface for commands that don't return a value
/// </summary>
/// <typeparam name="TCommand">The type of command</typeparam>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    /// <summary>Handles the specified <paramref name="command"/>.</summary>
    Task ExecuteAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler interface for commands that return a value
/// </summary>
/// <typeparam name="TCommand">The type of command</typeparam>
/// <typeparam name="TResponse">The type of response</typeparam>
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <summary>Handles the specified <paramref name="command"/> and returns a result.</summary>
    Task<TResponse> ExecuteAsync(TCommand command, CancellationToken cancellationToken = default);
}
