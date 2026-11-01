using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>Host mapping for expected failures; null preserves caller cancellation.</summary>
public interface IHttpExceptionMapper
{
	/// <summary>Maps an exception without exposing its message, body or stack.</summary>
	HttpFailure? Map(HttpContext context, Exception exception);
}
