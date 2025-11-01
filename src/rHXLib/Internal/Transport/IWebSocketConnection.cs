using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace rHXLib.Internal.Transport;

public interface IWebSocketConnection : IDisposable
{
    WebSocketState State { get; }
    Task SendBinaryAsync(ArraySegment<byte> payload, CancellationToken ct);
    Task<(WebSocketMessageType type, int count, bool endOfMessage)> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
}
