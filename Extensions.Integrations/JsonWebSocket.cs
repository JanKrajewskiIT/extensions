using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Extensions.Domain.Serialization;

namespace Extensions.Integrations;

public sealed class JsonWebSocket : IAsyncDisposable
{
	private readonly ClientWebSocket socket = new();
	private readonly int maximumMessageBytes;

	public JsonWebSocket(int maximumMessageBytes)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessageBytes);
		this.maximumMessageBytes = maximumMessageBytes;
		socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
	}

	public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => socket.ConnectAsync(uri, cancellationToken);

	public async ValueTask SendAsync<T>(T payload, CancellationToken cancellationToken)
	{
		var bytes = Encoding.UTF8.GetBytes(PayloadSerializer.Serialize(payload));
		await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
	}

	public async ValueTask<JsonDocument> ReceiveAsync(CancellationToken cancellationToken)
	{
		using var message = new MemoryStream();
		var buffer = new byte[16384];
		ValueWebSocketReceiveResult result;
		do
		{
			result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
			if (result.MessageType == WebSocketMessageType.Close)
			{
				throw new IOException("The provider closed the WebSocket; recovery is required.");
			}

			if (message.Length + result.Count > maximumMessageBytes)
			{
				throw new InvalidDataException("WebSocket message exceeds the configured limit.");
			}

			await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
		} while (!result.EndOfMessage);
		return JsonDocument.Parse(message.ToArray());
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			if (socket.State == WebSocketState.Open)
			{
				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
				await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "shutdown", timeout.Token);
			}
		}
		catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
		{
			socket.Abort();
		}
		finally
		{
			socket.Dispose();
		}
	}
}
