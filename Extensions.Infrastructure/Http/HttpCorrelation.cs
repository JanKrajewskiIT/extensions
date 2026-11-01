using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>Validated correlation identity shared by responses, middleware and host policies.</summary>
public static class HttpCorrelation
{
	/// <summary>The public correlation header.</summary>
	public const string HeaderName = "X-Correlation-Id";

	/// <summary>Returns the request identity, assigning a nonempty GUID when necessary.</summary>
	public static Guid GetOrCreate(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (context.Items[typeof(HttpCorrelation)] is not Guid correlation)
		{
			var incoming = context.Request.Headers[HeaderName].ToString();
			correlation = Guid.TryParse(incoming, out var parsed) && parsed != Guid.Empty ? parsed : Guid.NewGuid();
			context.Items[typeof(HttpCorrelation)] = correlation;
		}

		if (!context.Response.HasStarted)
		{
			context.Response.Headers[HeaderName] = correlation.ToString("D");
		}

		return correlation;
	}
}
