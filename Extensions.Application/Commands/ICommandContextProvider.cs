namespace Extensions.Application.Commands;

/// <summary>Authorizes an invocation and maps its command identity and trusted actor/scope explicitly.</summary>
public interface ICommandContextProvider<in TCommand>
{
    /// <summary>Must return the same context in the caller and every fresh transaction attempt scope.</summary>
    CommandContext Create(TCommand command);
}
