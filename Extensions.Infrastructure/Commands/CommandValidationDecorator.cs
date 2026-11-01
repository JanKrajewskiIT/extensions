using FluentValidation;
using FluentValidation.Results;

namespace Extensions.Infrastructure.Commands;

internal sealed class CommandValidationDecorator<TCommand>(IEnumerable<IValidator<TCommand>> validators)
{
    public async Task ValidateAsync(TCommand command, CancellationToken token)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TCommand>(command), token);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
