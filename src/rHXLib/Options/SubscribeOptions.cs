using System;

namespace rHXLib;

public sealed class SubscribeOptions
{
    public int ReceiveBufferBytes { get; set; } = 64 * 1024; // 64 KiB chunks
    public TimeSpan? ReconnectDelay { get; set; }
}

