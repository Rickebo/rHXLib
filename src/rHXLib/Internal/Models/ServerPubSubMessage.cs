using System;
using Newtonsoft.Json;

namespace rHXLib.Internal.Models;

internal sealed class ServerPubSubHeader
{
    [JsonProperty("topic")] public string Topic { get; set; } = string.Empty;
    [JsonProperty("username")] public string Username { get; set; } = string.Empty;
    [JsonProperty("sentAtUtc")] public DateTimeOffset SentAtUtc { get; set; }
}

internal sealed class ServerPubSubMessage
{
    [JsonProperty("header")] public ServerPubSubHeader Header { get; set; } = new();
    // Newtonsoft serializes byte[] as base64 strings in JSON
    [JsonProperty("payload")] public byte[] Payload { get; set; } = Array.Empty<byte>();

    public static bool TryParse(string json, out ServerPubSubMessage? message)
    {
        try
        {
            message = JsonConvert.DeserializeObject<ServerPubSubMessage>(json);
            return message != null;
        }
        catch
        {
            message = null;
            return false;
        }
    }
}
