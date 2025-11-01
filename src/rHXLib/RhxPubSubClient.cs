using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using rHXLib.Internal.Models;
using rHXLib.Internal.Transport;

namespace rHXLib;

public sealed class RhxPubSubClient : IRhxPubSubClient
{
    private readonly RhxLibOptions _options;
    private readonly ILogger<RhxPubSubClient>? _logger;
    private readonly IMessageSerializer _serializer;
    private readonly IWebSocketFactory _wsFactory;
    private readonly ITokenProvider _tokenProvider;
    private readonly AsyncRetryPolicy _connectRetry;

    private readonly ConcurrentDictionary<string, IWebSocketConnection> _publishSockets = new();

    public RhxPubSubClient(IOptions<RhxLibOptions> options,
                           ILogger<RhxPubSubClient>? logger = null,
                           IWebSocketFactory? wsFactory = null)
    {
        _options = options?.Value ?? new RhxLibOptions();
        _logger = logger;
        _serializer = _options.Serializer ?? new NewtonsoftJsonMessageSerializer();
        _wsFactory = wsFactory ?? new WebSocketFactory();
        _tokenProvider = new TokenProviderChain(
            new StaticTokenProvider(_options.Auth.AccessToken),
            new LauncherFileTokenProvider(_options.Auth.TokenFilePath),
            new OidcTokenProvider(_options.Auth)
        );

        _connectRetry = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)),
                (ex, delay, attempt, ctx) => _logger?.LogWarning(ex, "WS connect attempt {Attempt} failed; retrying in {Delay}", attempt, delay));
    }

    public async Task PublishAsync<T>(string topic, T message, PublishOptions? options = null, CancellationToken ct = default)
    {
        if (_options.ApiBaseUrl == null) throw new InvalidOperationException("RhxLibOptions.ApiBaseUrl must be configured.");

        var uri = BuildWsUri(_options.ApiBaseUrl, topic);
        var payload = _serializer.Serialize(message);
        var token = await _tokenProvider.GetAccessTokenAsync(ct).ConfigureAwait(false);

        var socket = await GetOrConnectPublisherSocketAsync(topic, uri, token, ct).ConfigureAwait(false);
        try
        {
            await socket.SendBinaryAsync(new ArraySegment<byte>(payload), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException || ex is ObjectDisposedException)
        {
            _logger?.LogWarning(ex, "Publish send failed; attempting one reconnect for topic {Topic}", topic);
            // Remove and dispose old socket if present
            if (_publishSockets.TryRemove(topic, out var old)) { try { old.Dispose(); } catch { } }
            var re = await _connectRetry.ExecuteAsync(async () =>
                await _wsFactory.ConnectAsync(uri, token, _options.KeepAliveInterval, _options.ConnectTimeout, ct).ConfigureAwait(false)
            ).ConfigureAwait(false);
            _publishSockets.AddOrUpdate(topic, re, (_, prev) => { try { prev?.Dispose(); } catch { } return re; });
            await re.SendBinaryAsync(new ArraySegment<byte>(payload), ct).ConfigureAwait(false);
        }
    }

    public async Task<IDisposable> SubscribeAsync<T>(string topic, Func<T, MessageContext, Task> handler, SubscribeOptions? options = null, CancellationToken ct = default)
    {
        if (_options.ApiBaseUrl == null) throw new InvalidOperationException("RhxLibOptions.ApiBaseUrl must be configured.");
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var disposed = 0;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(16 * 1024, options?.ReceiveBufferBytes ?? 64 * 1024));

        // Establish initial connection before returning to the caller
        IWebSocketConnection? initialConn = null;
        try
        {
            var initUri = BuildWsUri(_options.ApiBaseUrl, topic);
            var initToken = await _tokenProvider.GetAccessTokenAsync(ct).ConfigureAwait(false);
            initialConn = await _connectRetry.ExecuteAsync(async () =>
                await _wsFactory.ConnectAsync(initUri, initToken, _options.KeepAliveInterval, _options.ConnectTimeout, ct).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Initial subscribe connect failed for topic {Topic}", topic);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var ms = new System.IO.MemoryStream(capacity: 16 * 1024);
                while (!cts.IsCancellationRequested)
                {
                    // Use initial connection if available; otherwise connect now
                    IWebSocketConnection conn;
                    if (initialConn != null)
                    {
                        conn = initialConn;
                        initialConn = null;
                    }
                    else
                    {
                        var uri = BuildWsUri(_options.ApiBaseUrl!, topic);
                        var token = await _tokenProvider.GetAccessTokenAsync(cts.Token).ConfigureAwait(false);
                        conn = await _connectRetry.ExecuteAsync(async () =>
                            await _wsFactory.ConnectAsync(uri, token, _options.KeepAliveInterval, _options.ConnectTimeout, cts.Token).ConfigureAwait(false)
                        ).ConfigureAwait(false);
                    }

                    try
                    {
                        while (!cts.IsCancellationRequested && conn.State == WebSocketState.Open)
                        {
                            ms.SetLength(0);
                            int total = 0;
                            ValueWebSocketReceiveResult? result = null;
                            do
                            {
                                var seg = new ArraySegment<byte>(buffer);
                                var rr = await conn.ReceiveAsync(seg, cts.Token).ConfigureAwait(false);
                                result = new ValueWebSocketReceiveResult(rr.count, rr.type, rr.endOfMessage);
                                if (rr.type == WebSocketMessageType.Close)
                                {
                                    try { await conn.CloseAsync(cts.Token).ConfigureAwait(false); } catch { }
                                    break;
                                }
                                if (rr.count > 0)
                                {
                                    ms.Write(buffer, 0, rr.count);
                                    total += rr.count;
                                    if (total > 8 * 1024 * 1024)
                                        throw new InvalidOperationException($"Incoming message too large: {total} bytes");
                                }
                            } while (!result.Value.EndOfMessage);

                            if (result.HasValue && result.Value.MessageType == WebSocketMessageType.Text && ms.Length > 0)
                            {
                                var jsonStr = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                                if (ServerPubSubMessage.TryParse(jsonStr, out var msg) && msg != null)
                                {
                                    try
                                    {
                                        var value = _serializer.Deserialize<T>(msg.Payload);
                                        var ctx = new MessageContext
                                        {
                                            Topic = msg.Header.Topic,
                                            Username = msg.Header.Username,
                                            TimestampUtc = msg.Header.SentAtUtc,
                                        };
                                        await handler(value, ctx).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.LogError(ex, "Error in subscriber handler for topic {Topic}", topic);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (WebSocketException wse)
                    {
                        _logger?.LogDebug(wse, "WebSocket exception on topic {Topic}", topic);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "WebSocket error on topic {Topic}", topic);
                    }
                    finally
                    {
                        try { conn.Dispose(); } catch { }
                    }

                    if (cts.IsCancellationRequested) break;
                    if (options?.ReconnectDelay is TimeSpan d)
                    {
                        try { await Task.Delay(d, cts.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { break; }
                        continue; // reconnect
                    }
                    break; // no reconnect configured
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });

        return new DisposableAction(() =>
        {
            if (System.Threading.Interlocked.Exchange(ref disposed, 1) == 0)
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }
        });
    }

    private async Task<IWebSocketConnection> GetOrConnectPublisherSocketAsync(string topic, Uri uri, string? token, CancellationToken ct)
    {
        if (_publishSockets.TryGetValue(topic, out var existing) && existing.State == WebSocketState.Open)
            return existing;

        var conn = await _connectRetry.ExecuteAsync(async () =>
            await _wsFactory.ConnectAsync(uri, token, _options.KeepAliveInterval, _options.ConnectTimeout, ct).ConfigureAwait(false)
        ).ConfigureAwait(false);

        _publishSockets.AddOrUpdate(topic, conn, (_, old) => { try { old?.Dispose(); } catch { } return conn; });
        return conn;
    }

    private static Uri BuildWsUri(Uri apiBaseUrl, string topic)
    {
        // apiBaseUrl ends with /api; WS is /api/ws/pubsub/{topic}
        var builder = new UriBuilder(apiBaseUrl);
        builder.Scheme = builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var basePath = builder.Path.TrimEnd('/');
        builder.Path = $"{basePath}/ws/pubsub/{Uri.EscapeDataString(topic)}";
        return builder.Uri;
    }

    private readonly struct ValueWebSocketReceiveResult
    {
        public ValueWebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage)
        { Count = count; MessageType = messageType; EndOfMessage = endOfMessage; }
        public int Count { get; }
        public WebSocketMessageType MessageType { get; }
        public bool EndOfMessage { get; }
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _action;
        public DisposableAction(Action action) { _action = action; }
        public void Dispose() => _action();
    }
}
