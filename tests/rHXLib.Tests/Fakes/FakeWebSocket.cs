using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using rHXLib.Internal.Transport;

namespace rHXLib.Tests.Fakes;

internal sealed class FakeWebSocketConnection : IWebSocketConnection
{
    private readonly BlockingCollection<(WebSocketMessageType type, byte[] data, bool end)> _in = new();
    private readonly BlockingCollection<byte[]> _sent = new();
    private volatile WebSocketState _state = WebSocketState.Open;

    public WebSocketState State => _state;

    public void EnqueueText(string text)
        => _in.Add((WebSocketMessageType.Text, Encoding.UTF8.GetBytes(text), true));

    public byte[]? TryDequeueSent(int milliseconds = 0)
    {
        _sent.TryTake(out var d, milliseconds);
        return d;
    }

    public Task SendBinaryAsync(ArraySegment<byte> payload, CancellationToken ct)
    {
        _sent.Add(payload.ToArray());
        return Task.CompletedTask;
    }

    public Task<(WebSocketMessageType type, int count, bool endOfMessage)> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct)
    {
        var next = _in.Take(ct);
        Array.Copy(next.data, 0, buffer.Array!, buffer.Offset, next.data.Length);
        return Task.FromResult((next.type, next.data.Length, next.end));
    }

    public Task CloseAsync(CancellationToken ct)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _state = WebSocketState.Closed;
        _in.Dispose();
        _sent.Dispose();
    }
}

internal sealed class FakeWebSocketFactory : IWebSocketFactory
{
    public FakeWebSocketConnection LastConnection { get; private set; } = new();

    public Task<IWebSocketConnection> ConnectAsync(Uri uri, string? bearerToken, TimeSpan keepAlive, TimeSpan timeout, CancellationToken ct)
    {
        LastConnection = new FakeWebSocketConnection();
        return Task.FromResult<IWebSocketConnection>(LastConnection);
    }
}

