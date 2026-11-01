using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>HTTP command identity validation; durable receipt ownership remains with the application.</summary>
public static class HttpIdempotency
{
	/// <summary>The command identity header.</summary>
	public const string HeaderName = "Idempotency-Key";

	/// <summary>Requires one nonempty GUID header equal to the command identity.</summary>
	public static string Require(HttpContext context, Guid commandId)
	{
		ArgumentNullException.ThrowIfNull(context);
		var key = context.Request.Headers[HeaderName].ToString();
		if (commandId == Guid.Empty || !TryRead(context, out var keyId) || keyId != commandId)
		{
			throw new ArgumentException("Idempotency-Key must equal the nonempty command identity.", nameof(commandId));
		}

		return key;
	}

	/// <summary>Requires a nonempty command identity when no transport command has been bound yet.</summary>
	public static Guid Require(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return TryRead(context, out var id)
			? id : throw new ArgumentException("Idempotency-Key must be a nonempty command identity.", nameof(context));
	}

	/// <summary>Reads a validated header identity without selecting an application-specific refusal code.</summary>
	public static bool TryRead(HttpContext context, out Guid identity)
	{
		ArgumentNullException.ThrowIfNull(context);
		return Guid.TryParse(context.Request.Headers[HeaderName].ToString(), out identity) && identity != Guid.Empty;
	}
}
