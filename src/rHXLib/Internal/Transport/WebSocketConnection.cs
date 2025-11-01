using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace rHXLib.Internal.Transport;

internal sealed class WebSocketConnection : IWebSocketConnection
{
    private readonly ClientWebSocket _socket;

    public WebSocketConnection(ClientWebSocket socket)
    {
        _socket = socket;
    }

    public WebSocketState State => _socket.State;

    public Task SendBinaryAsync(ArraySegment<byte> payload, CancellationToken ct)
        => _socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: ct);

    public async Task<(WebSocketMessageType type, int count, bool endOfMessage)> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct)
    {
        var res = await _socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
        return (res.MessageType, res.Count, res.EndOfMessage);
    }

    public Task CloseAsync(CancellationToken ct)
        => _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);

    public void Dispose()
    {
        try { _socket.Abort(); } catch { }
        _socket.Dispose();
    }
}
