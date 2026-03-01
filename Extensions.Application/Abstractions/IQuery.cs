namespace Extensions.Application.Abstractions;

/// <summary>
/// Marker interface for queries that return a value
/// </summary>
/// <typeparam name="TResponse">The type of response</typeparam>
public interface IQuery<out TResponse>;
