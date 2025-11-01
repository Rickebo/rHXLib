using System;
using System.Collections.Generic;

namespace rHXLib;

public sealed class MessageContext
{
    public string Topic { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? MessageId { get; set; }
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}

