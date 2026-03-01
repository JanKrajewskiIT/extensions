namespace Extensions.Application.Abstractions;

/// <summary>
/// Marker interface for commands that don't return a value
/// </summary>
public interface ICommand;

/// <summary>
/// Marker interface for commands that return a value
/// </summary>
/// <typeparam name="TResponse">The type of response</typeparam>
public interface ICommand<out TResponse>;
