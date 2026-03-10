using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Infrastructure.Validation;

/// <summary>
/// Endpoint filter that automatically validates request parameters using FluentValidation.
/// Resolves <see cref="IValidator{T}"/> from DI and calls ValidateAndThrowAsync before the endpoint executes.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
	/// <inheritdoc />
	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();

		if (validator is not null)
		{
			var argument = context.Arguments.OfType<T>().FirstOrDefault();

			if (argument is not null)
			{
				await validator.ValidateAndThrowAsync(argument, context.HttpContext.RequestAborted);
			}
		}

		return await next(context);
	}
}

/// <summary>
/// Extension method to add automatic validation to an endpoint.
/// </summary>
public static class ValidationFilterExtensions
{
	/// <summary>
	/// Adds automatic FluentValidation to the endpoint for the specified request type.
	/// If a validator is registered in DI, it will run before the endpoint handler.
	/// </summary>
	public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class
	{
		return builder.AddEndpointFilter<ValidationFilter<T>>();
	}
}
