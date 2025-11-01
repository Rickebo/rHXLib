using System;

namespace rHXLib;

public sealed class RhxLibOptions
{
    // Example: https://rhx.rickebo.com/api
    public Uri? ApiBaseUrl { get; set; }

    public IMessageSerializer? Serializer { get; set; }

    public AuthOptions Auth { get; set; } = new();

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class AuthOptions
{
    public string? AccessToken { get; set; }
    public string? TokenFilePath { get; set; }

    // OIDC fallback
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public bool UseDeviceCode { get; set; } = true;
}

