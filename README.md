## rHXLib

Typed publish/subscribe client for rHX2 topics over WebSockets. Supports pluggable serialization (JSON by default) and a token auth chain:

- Static token (provided in code)
- rHX2 launcher token file at `%APPDATA%/rHX/launcher/auth.json`
- OIDC Device Code flow (if Authority/ClientId/Scopes are configured)

### Install

This repository builds a `netstandard2.0` library. It works in .NET Framework 4.6.1+ and modern .NET.

### Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using rHXLib;

var services = new ServiceCollection()
    .AddRhxLib(o =>
    {
        o.ApiBaseUrl = new Uri("https://rhx.rickebo.com/api");
        // Optional: set a token directly
        // o.Auth.AccessToken = "...";
        // Optional OIDC fallback
        // o.Auth.Authority = "https://<issuer_root>/";
        // o.Auth.ClientId = "<client-id>";
        // o.Auth.Scopes = new[] { "openid", "profile", "email", "offline_access" };
    });

var sp = services.BuildServiceProvider();
var client = sp.GetRequiredService<IRhxPubSubClient>();

// Subscribe for typed messages
var sub = await client.SubscribeAsync<MyEvent>("orders",
    async (evt, ctx) => Console.WriteLine($"{ctx.TimestampUtc:o} {ctx.Username}: {evt.Id}"));

// Publish a typed message
await client.PublishAsync("orders", new MyEvent { Id = Guid.NewGuid().ToString() });

// ... later
sub.Dispose();

public record MyEvent { public string Id { get; init; } = string.Empty; }
```

### Notes

- Delivery semantics: at-most-once. Broker does not redeliver.
- The broker expects binary payloads for incoming client publishes; broadcasts are JSON text frames with a `header` and base64 `payload`.
- Token file format follows the launcher: JSON with `access_token` (and optional `refresh_token`, `id_token`).

