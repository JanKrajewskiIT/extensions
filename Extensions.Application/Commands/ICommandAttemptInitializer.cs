namespace Extensions.Application.Commands;

/// <summary>Rebinds trusted scoped workload state before a fresh retry scope independently authorizes the command.</summary>
public interface ICommandAttemptInitializer
{
    /// <summary>Binds scoped state only; must not perform external effects or grant authority from untrusted data.</summary>
    void Initialize(CommandContext context);
}
