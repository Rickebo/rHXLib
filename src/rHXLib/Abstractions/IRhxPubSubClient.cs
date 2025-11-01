using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rHXLib;

public interface IRhxPubSubClient
{
    Task PublishAsync<T>(string topic, T message, PublishOptions? options = null, CancellationToken ct = default);

    Task<IDisposable> SubscribeAsync<T>(string topic, Func<T, MessageContext, Task> handler, SubscribeOptions? options = null, CancellationToken ct = default);
}

