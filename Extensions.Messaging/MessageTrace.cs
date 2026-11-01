using System.Diagnostics;
using System.Text;
using Confluent.Kafka;

namespace Extensions.Messaging;

internal static class MessageTrace
{
	internal const string Header = "traceparent";

	public static Headers Headers(string? traceParent)
	{
		var headers = new Headers();
		if (traceParent is not null && ActivityContext.TryParse(traceParent, null, out _))
		{
			headers.Add(Header, Encoding.UTF8.GetBytes(traceParent));
		}

		return headers;
	}

	public static Activity? Start(Headers? headers)
	{
		if (headers is null || !headers.TryGetLastBytes(Header, out var bytes) || bytes is null || bytes.Length > 128)
		{
			return null;
		}

		var parent = Encoding.UTF8.GetString(bytes);
		return ActivityContext.TryParse(parent, null, out _)
			? new Activity("messaging.process").SetIdFormat(ActivityIdFormat.W3C).SetParentId(parent).Start()
			: null;
	}
}
