using System;
using System.Threading;
using System.Threading.Tasks;

namespace rHXLib.Internal.Transport;

public interface IWebSocketFactory
{
    Task<IWebSocketConnection> ConnectAsync(Uri uri, string? bearerToken, TimeSpan keepAlive, TimeSpan timeout, CancellationToken ct);
}
