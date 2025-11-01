using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace rHXLib.Internal.Transport;

internal sealed class WebSocketFactory : IWebSocketFactory
{
    public async Task<IWebSocketConnection> ConnectAsync(Uri uri, string? bearerToken, TimeSpan keepAlive, TimeSpan timeout, CancellationToken ct)
    {
        var cws = new ClientWebSocket
        {
            Options =
            {
                KeepAliveInterval = keepAlive
            }
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            cws.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        await cws.ConnectAsync(uri, cts.Token).ConfigureAwait(false);
        return new WebSocketConnection(cws);
    }
}
